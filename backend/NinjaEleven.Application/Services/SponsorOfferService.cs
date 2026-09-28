using Microsoft.Extensions.Logging;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Sponsors;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Services;

/// <summary>
/// Generates sponsor offers for clubs and settles shirt deals.
///
/// A club without a shirt deal is offered a handful of sponsors at random from the master
/// pool; a club with a deal that is paid off can sign a new one. The service never invents
/// sponsors of its own — it draws from the catalog the seeder laid down — so a sponsor's
/// name and colour are stable facts rather than strings conjured by the moment.
/// </summary>
public class SponsorOfferService
{
    private readonly ISponsorRepository _sponsors;
    private readonly ISponsorContractRepository _contracts;
    private readonly ITeamRepository _teams;
    private readonly IFinanceRepository _finance;
    private readonly ISeasonRepository _seasons;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SponsorOfferService> _logger;

    public SponsorOfferService(
        ISponsorRepository sponsors,
        ISponsorContractRepository contracts,
        ITeamRepository teams,
        IFinanceRepository finance,
        ISeasonRepository seasons,
        IUnitOfWork unitOfWork,
        ILogger<SponsorOfferService> logger)
    {
        _sponsors = sponsors;
        _contracts = contracts;
        _teams = teams;
        _finance = finance;
        _seasons = seasons;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// The sponsor book of a team for a season: the deal it is playing under, and a fresh set
    /// of candidates when it is free or when its deal has one match left (a renewal window).
    /// </summary>
    public async Task<SponsorBook> GetBookAsync(
        Guid teamId,
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        var team = await _teams.GetAsync(teamId, cancellationToken)
            ?? throw new EntityNotFoundException("Team", teamId);

        _ = await _seasons.GetAsync(seasonId, cancellationToken)
            ?? throw new EntityNotFoundException("Season", seasonId);

        var active = await _contracts.GetActiveByTeamAsync(teamId, cancellationToken);

        // A club whose deal is down to its last match is offered a new set: the next match
        // will be unsponsored if the manager does not act.
        var isExpiring = active is { MatchesLeft: 1 };

        SponsorContractSlim? current = null;
        if (active is not null)
        {
            var sponsor = await _sponsors.GetAsync(active.SponsorId, cancellationToken);
            current = new SponsorContractSlim
            {
                ContractId = active.Id,
                SponsorId = active.SponsorId,
                 SponsorName = sponsor?.Name ?? "Desconhecido",
                 SponsorIndustry = sponsor?.Industry ?? string.Empty,
                 SponsorColor = sponsor?.Color ?? "#f2d34f",
                PerMatchFee = active.PerMatchFee,
                ContractMatches = active.ContractMatches,
                MatchesPlayed = active.MatchesPlayed
            };
        }

        var candidates = (isExpiring || active is null)
            ? await DrawCandidatesAsync(cancellationToken)
            : [];

        return new SponsorBook
        {
            TeamId = teamId,
            SeasonId = seasonId,
            Current = current,
            Candidates = candidates
        };
    }

    /// <summary>
    /// Signs a new shirt deal for a club.
    ///
    /// A club cannot have two active deals: the current one must be paid off (or expired)
    /// before another can take its place. The reference is the sponsor so a club that signs
    /// the same sponsor twice — once for a deal that ran out, once again — leaves two
    /// contracts and not a duplicate of one.
    /// </summary>
    public async Task<SponsorBook> SignAsync(
        Guid teamId,
        Guid seasonId,
        Guid sponsorId,
        decimal? perMatchFee = null,
        int? contractMatches = null,
        CancellationToken cancellationToken = default)
    {
        var team = await _teams.GetAsync(teamId, cancellationToken)
            ?? throw new EntityNotFoundException("Team", teamId);

        _ = await _seasons.GetAsync(seasonId, cancellationToken)
            ?? throw new EntityNotFoundException("Season", seasonId);

        var existing = await _contracts.GetActiveByTeamAsync(teamId, cancellationToken);
        if (existing is not null && existing.IsActive)
        {
            throw new DomainValidationException(
                "TeamHasActiveSponsor",
                $"{team.Name} already has an active sponsor deal and cannot sign another.");
        }

        var sponsor = await _sponsors.GetAsync(sponsorId, cancellationToken)
            ?? throw new EntityNotFoundException("Sponsor", sponsorId);

        var fee = perMatchFee ?? DrawFee();
        var length = contractMatches ?? SponsorRules.BaseOfferLength;

        var contract = SponsorContract.Sign(sponsorId, teamId, seasonId, fee, length);

        await _contracts.AddAsync(contract, cancellationToken);
        team.SignSponsorContract(contract);
        _teams.Update(team);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "{TeamName} signed a shirt deal with {SponsorName}: {Fee} limos per match for {Length} matches.",
            team.Name, sponsor.Name, fee, length);

        return await GetBookAsync(teamId, seasonId, cancellationToken);
    }

    /// <summary>
    /// Records one match played under a club's shirt deal and pays the sponsor for it.
    ///
    /// The payment is written against the match: a match settled twice — by the tick that
    /// blew the whistle and by one that arrives after it — leaves one line in the book and
    /// not two, because the finance guard recognises the match id. A deal that runs out on
    /// this goal is expired, and the club is free to sign another.
    ///
    /// The matchDay is passed in by the caller because MatchService already resolved it for
    /// wages and gate receipts — a second lookup here would re-derive what is already known.
    /// </summary>
    public async Task<MatchSponsorPayment?> PayPerMatchAsync(
        Guid teamId,
        Guid matchId,
        Guid seasonId,
        int? matchDayNumber,
        CancellationToken cancellationToken = default)
    {
        var active = await _contracts.GetActiveByTeamAsync(teamId, cancellationToken);
        if (active is null || !active.IsActive)
        {
            return null;
        }

        // A sponsor payment must happen once per match. The guard is the match id.
        if (await _finance.ExistsForMatchAsync(
                teamId, matchId, FinanceMovementKind.Sponsorship, cancellationToken))
        {
            return null;
        }

        var sponsor = await _sponsors.GetAsync(active.SponsorId, cancellationToken);
        var fee = active.PerMatchFee;

        active.RecordMatchPlayed();
        _contracts.Update(active);

        var last = await _finance.GetLastAsync(teamId, cancellationToken);
        var sequence = (last?.Sequence ?? 0) + 1;

        var line = FinanceMovement.Create(
            teamId,
            seasonId,
            sequence,
            matchDayNumber,
            FinanceMovementKind.Sponsorship,
            $"Patrocínio {sponsor?.Name ?? "Da Silva Esportes"}",
            fee,
            balanceBefore: last?.BalanceAfter ?? 0m,
            matchId,
            $"sponsor:{active.Id}");

        await _finance.AddAsync(line, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Paid {TeamId} {Fee} limos from {SponsorName} for match {MatchId}.",
            teamId, fee, sponsor?.Name ?? "?", matchId);

        return new MatchSponsorPayment
        {
            ContractId = active.Id,
            SponsorName = sponsor?.Name ?? string.Empty,
            Amount = fee,
            IsDealExpired = active.Status is SponsorContractStatus.Expired
        };
    }

    /// <summary>
    /// The sponsor that was paid for one match, if a deal was active. Null when the club
    /// had no sponsor.
    /// </summary>
    public sealed class MatchSponsorPayment
    {
        public Guid ContractId { get; init; }
        public string SponsorName { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public bool IsDealExpired { get; init; }
    }

    private async Task<IReadOnlyList<SponsorOffer>> DrawCandidatesAsync(CancellationToken cancellationToken)
    {
        var all = await _sponsors.ListAsync(cancellationToken);
        if (all.Count == 0)
        {
            return [];
        }

        var random = Random.Shared;
        var pool = all.OrderBy(_ => random.Next()).Take(SponsorRules.MaxCandidateOffers).ToList();

        return pool.Select(sponsor => new SponsorOffer
        {
            SponsorId = sponsor.Id,
            Name = sponsor.Name,
            Industry = sponsor.Industry,
            Color = sponsor.Color,
            PerMatchFee = DrawFee(),
            ContractMatches = SponsorRules.BaseOfferLength + random.Next(SponsorRules.BaseOfferLength)
        }).ToList();
    }

    private static decimal DrawFee()
    {
        var random = Random.Shared;
        var span = SponsorRules.MaxPerMatchFee - SponsorRules.MinPerMatchFee;
        var sample = (decimal)random.NextDouble();
        var fee = SponsorRules.MinPerMatchFee + span * sample;
        return Math.Round(fee / 1000m) * 1000m;
    }
}
