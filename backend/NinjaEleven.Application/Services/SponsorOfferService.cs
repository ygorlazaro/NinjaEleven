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
    private readonly ICompetitionRepository _competitions;
    private readonly SponsorClubFactsService _facts;
    private readonly InboxService _inbox;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SponsorOfferService> _logger;

    public SponsorOfferService(
        ISponsorRepository sponsors,
        ISponsorContractRepository contracts,
        ITeamRepository teams,
        IFinanceRepository finance,
        ISeasonRepository seasons,
        ICompetitionRepository competitions,
        SponsorClubFactsService facts,
        InboxService inbox,
        IUnitOfWork unitOfWork,
        ILogger<SponsorOfferService> logger)
    {
        _sponsors = sponsors;
        _contracts = contracts;
        _teams = teams;
        _finance = finance;
        _seasons = seasons;
        _competitions = competitions;
        _facts = facts;
        _inbox = inbox;
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
        var isExpiring = active is { } deal
                          && deal.MatchesLeft <= SponsorRules.MatchesLeftBeforeRenewalWindow;

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
            ? await DrawCandidatesAsync(teamId, seasonId, cancellationToken)
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

        // Both numbers are worked out here and neither is accepted from the caller. A price a
        // client can send is a price a screen made up, and a manager budgeting his season
        // against a number the book does not carry is budgeting against a lie; the same goes
        // for the length, which the club's own seed already decides.
        var fee = await PriceForAsync(sponsor, teamId, seasonId, cancellationToken);

        if (fee <= 0m)
        {
            throw new DomainValidationException(
                "SponsorPriceIsZero",
                $"{sponsor.Name} would pay nothing for a shirt on {team.Name} this season.");
        }

        var length = ContractLengthFor(sponsor, teamId, seasonId);

        var contract = SponsorContract.Sign(sponsorId, teamId, seasonId, fee, length);

        await _contracts.AddAsync(contract, cancellationToken);
        team.SignSponsorContract(contract);
        _teams.Update(team);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "{TeamName} signed a shirt deal with {SponsorName}: {Fee} limos per match for {Length} matches.",
            team.Name, sponsor.Name, fee, length);

        // The deal is announced the day it is signed rather than the day the first instalment
        // lands. A sponsor pays per match, so the money would otherwise only show up in the
        // statement a week later, with a name on the shirt the manager cannot place — and the
        // two halves of the same news, the contract and the expiry, belong in the same box.
        await _inbox.PostSponsorSignedAsync(
            new SponsorSignedFacts
            {
                RecipientTeamId = teamId,
                ClubName = team.Name,
                SponsorId = sponsor.Id,
                SponsorName = sponsor.Name,
                ContractId = contract.Id,
                PerMatchFee = fee,
                ContractMatches = length
            },
            cancellationToken);

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

        // The domain says whether this call was the one that ended the deal, and the answer is
        // asked here rather than recomputed from the counts: a club whose last instalment has
        // just been paid is playing the next match with nobody's name on the shirt, and nothing
        // else in the game would ever tell it so.
        var justExpired = active.RecordMatchPlayed();
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

        // The instalment itself is not a message. A sponsor pays per match, so reporting it
        // would be one message per matchday saying a number the manager signed up for, and it
        // is in the statement with the gate and the wages. What the box does carry is the
        // deal itself — signed above, expired below — because those are the two moments a
        // manager cannot work out from a balance.

        if (justExpired)
        {
            await _inbox.PostSponsorExpiryAsync(
                new SponsorExpiryFacts
                {
                    RecipientTeamId = teamId,
                    ClubName = (await _teams.GetAsync(teamId, cancellationToken))?.Name
                               ?? teamId.ToString(),
                    SponsorId = active.SponsorId,
                    SponsorName = sponsor?.Name ?? "o patrocinador",
                    ContractId = active.Id,
                    PerMatchFee = active.PerMatchFee,
                    ContractMatches = active.ContractMatches
                },
                cancellationToken);
        }

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

    private async Task<IReadOnlyList<SponsorOffer>> DrawCandidatesAsync(
        Guid teamId,
        Guid seasonId,
        CancellationToken cancellationToken)
    {
        var tier = await TierOfAsync(teamId, seasonId, cancellationToken);

        // A club in no division of this season has no shirt to sell, and there is nothing a
        // sponsor would be quoting for.
        if (tier is not { } divisionTier)
        {
            return [];
        }

        var all = await _sponsors.ListAsync(cancellationToken);

        if (all.Count == 0)
        {
            return [];
        }

        // One read of the football and one of the ledger, for the whole catalog: which clubs
        // each sponsor already has, and which of the season's divisions those clubs are in.
        var portfolio = await _contracts.ListActiveBySponsorIdsAsync(
            all.Select(sponsor => sponsor.Id),
            cancellationToken);

        var facts = await _facts.ReadAsync(seasonId, cancellationToken);
        var tierOfTeam = await _facts.ReadPlacementsAsync(seasonId, cancellationToken);

        ClubSponsorFacts? clubFacts = facts.GetValueOrDefault(teamId);

        // A club the standings could not place is priced as the average club rather than as a
        // club with nothing: a missing line on a table is a table that has not been built yet,
        // and a price of nothing would be the one reading of it a sponsor would not act on.
        var appeal = clubFacts is null ? 1.0 : SponsorPricing.Appeal(clubFacts);

        var divisions = tierOfTeam
            .Where(entry => entry.Value.Tier is not null)
            .ToDictionary(entry => entry.Key, entry => entry.Value.Tier!.Value);

        var offers = new List<SponsorOffer>();

        foreach (var sponsor in all)
        {
            if (!WantsThisClub(sponsor, divisionTier, appeal, portfolio, divisions))
            {
                continue;
            }

            var held = portfolio.TryGetValue(sponsor.Id, out var deals) ? deals.Count : 0;

            var priced = (clubFacts ?? new ClubSponsorFacts()) with { SponsorClubs = held };

            offers.Add(new SponsorOffer
            {
                SponsorId = sponsor.Id,
                Name = sponsor.Name,
                Industry = sponsor.Industry,
                Color = sponsor.Color,
                Weight = sponsor.Weight,
                ClubsSponsored = held,
                MaxClubs = sponsor.MaxClubs,
                PerMatchFee = SponsorPricing.FeeFor(divisionTier, sponsor, priced),
                ContractMatches = ContractLengthFor(sponsor, teamId, seasonId)
            });
        }

        return offers
            .OrderByDescending(offer => offer.PerMatchFee)
            .ThenBy(offer => offer.Name, StringComparer.OrdinalIgnoreCase)
            .Take(SponsorRules.CandidateOffers)
            .ToList();
    }

    /// <summary>
    /// Whether a company would put its name on this club at all.
    ///
    /// <para>
    /// Four reasons to say no, and each of them is a reason a real sponsor has: the club plays
    /// below the divisions it works in, the club is not the kind of club its name costs
    /// nothing to hang on, its slate is already full, or it already has a club in this
    /// division — a brand does not put two shirts in the same championship.
    /// </para>
    /// </summary>
    private static bool WantsThisClub(
        Sponsor sponsor,
        int tier,
        double appeal,
        IReadOnlyDictionary<Guid, IReadOnlyList<SponsorContract>> portfolio,
        IReadOnlyDictionary<Guid, int> tierOfTeam)
    {
        if (tier > sponsor.MaxTier)
        {
            return false;
        }

        if (appeal < sponsor.MinAppeal)
        {
            return false;
        }

        if (!portfolio.TryGetValue(sponsor.Id, out var deals) || deals.Count == 0)
        {
            return true;
        }

        if (deals.Count >= sponsor.MaxClubs)
        {
            return false;
        }

        // One shirt per division. A club whose tier this sponsor's own book cannot say is
        // treated as a clash rather than as a free run: a sponsor finding out afterwards that
        // it is on two shirts in one championship is worse than one missing an offer.
        return deals.All(contract =>
            tierOfTeam.TryGetValue(contract.TeamId, out var heldTier) && heldTier != tier);
    }

    /// <summary>
    /// What this sponsor pays this club, without asking it to be offered.
    ///
    /// <para>
    /// It is the same calculation the list is drawn from, so a club that signs without looking
    /// is paid the price the list would have shown rather than the price a formula invented on
    /// the way in.
    /// </para>
    /// </summary>
    private async Task<decimal> PriceForAsync(
        Sponsor sponsor,
        Guid teamId,
        Guid seasonId,
        CancellationToken cancellationToken)
    {
        var tier = await TierOfAsync(teamId, seasonId, cancellationToken);

        if (tier is not { } divisionTier)
        {
            return 0m;
        }

        var facts = await _facts.ReadAsync(seasonId, cancellationToken);
        var portfolio = await _contracts.ListActiveBySponsorIdsAsync(
            [sponsor.Id],
            cancellationToken);

        var held = portfolio.TryGetValue(sponsor.Id, out var deals) ? deals.Count : 0;

        ClubSponsorFacts? clubFacts = facts.GetValueOrDefault(teamId);

        var club = (clubFacts ?? new ClubSponsorFacts()) with { SponsorClubs = held };

        return SponsorPricing.FeeFor(divisionTier, sponsor, club);
    }

    /// <summary>
    /// How many matches an offer is for, drawn from the club's own seed rather than a random
    /// number, so the same club is offered the same length all week and a different one next
    /// week.
    /// </summary>
    private int ContractLengthFor(Sponsor sponsor, Guid teamId, Guid seasonId)
    {
        var random = new Random(SeedFor(teamId, seasonId, sponsor.Id));

        return SponsorRules.BaseOfferLength + random.Next(SponsorRules.BaseOfferLength);
    }

    /// <summary>
    /// The seed of a club's week in the sponsor market.
    ///
    /// <para>
    /// It is drawn rather than rolled because a length that changed every time a screen was
    /// reopened is not a deal anybody could decide anything with: the same company would
    /// offer the same shirt for ten matches on one visit and for fifteen on the next. Keyed on
    /// the club, the season and the sponsor, so each pair gets its own stream.
    /// </para>
    /// <para>
    /// The list of candidates is not drawn at all — it is every company that would take the
    /// club, ordered by what it pays — so a club's shortlist changes when the club changes,
    /// and not because a page was visited.
    /// </para>
    /// </summary>
    private static int SeedFor(Guid teamId, Guid seasonId, Guid sponsorId)
    {
        var key = $"{teamId:N}{seasonId:N}{sponsorId:N}";

        return key.Aggregate(17, (hash, character) => unchecked(hash * 31 + character))
            & int.MaxValue;
    }

    private async Task<int?> TierOfAsync(Guid teamId, Guid seasonId, CancellationToken cancellationToken)
    {
        var standing = await _competitions.GetDivisionSeasonForTeamAsync(
            teamId, seasonId, cancellationToken);

        return standing?.Tier;
    }
}
