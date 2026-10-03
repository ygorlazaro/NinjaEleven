using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Services;

/// <summary>
/// One club's building site, started by the world rather than by a person.
/// </summary>
public record NpcStadiumBuilt(
    Guid ClubId,
    int Seats,
    decimal Cost,
    int Rounds,
    NpcStadiumVerdict Verdict);

/// <summary>
/// The grounds of the clubs nobody is running, looked at every five rounds.
///
/// <para>
/// The decision itself belongs to <see cref="NpcStadiumPolicy"/> in the domain, and this service
/// is the part that has to go and find out what the decision needs. Everything the country is
/// judged on is read as a set: sixty-four grounds, sixty-four crowds, sixty-four books and one
/// country's wage bill. Asking per club would be sixty-four questions about each of the four,
/// and this is a pass that runs on the world's own bookkeeping.
/// </para>
///
/// <para>
/// It is idempotent in the way the rest of the world's housekeeping is, because
/// <see cref="CompetitionExecutionService"/> closes the same day from two directions and a hand
/// on the world can press it as often as a manager likes. A ground that is already a building
/// site is skipped, and a club that has already approved a project at this very round is
/// skipped as well — the second guard is what stops a day that settles a project and then looks
/// at the ground settling it from approving the next one with the same money.
/// </para>
/// </summary>
public class NpcStadiumService
{
    private readonly ITeamRepository _teams;
    private readonly IStadiumRepository _grounds;
    private readonly IStadiumConstructionRepository _constructions;
    private readonly ITeamFanBaseRepository _fanBases;
    private readonly IMatchRepository _matches;
    private readonly IFinanceRepository _finance;
    private readonly StadiumService _stadium;

    public NpcStadiumService(
        ITeamRepository teams,
        IStadiumRepository grounds,
        IStadiumConstructionRepository constructions,
        ITeamFanBaseRepository fanBases,
        IMatchRepository matches,
        IFinanceRepository finance,
        StadiumService stadium)
    {
        _teams = teams;
        _grounds = grounds;
        _constructions = constructions;
        _fanBases = fanBases;
        _matches = matches;
        _finance = finance;
        _stadium = stadium;
    }

    /// <summary>
    /// Looks at the country and starts what it has decided, returning everything it approved.
    /// </summary>
    /// <param name="seasonId">The season the decision is made in.</param>
    /// <param name="playedThroughRound">
    /// The highest championship round played. The schedule is a fact about this number and not
    /// about the clock: the same day read twice is the same pass, and a day the world owes and
    /// has not played is not a round a ground has been a building site through.
    /// </param>
    public async Task<IReadOnlyList<NpcStadiumBuilt>> BuildWhereTheWorldDecidesAsync(
        Guid seasonId,
        int playedThroughRound,
        CancellationToken cancellationToken = default)
    {
        if (!NpcStadiumPolicy.IsBuildingDay(playedThroughRound))
        {
            return Array.Empty<NpcStadiumBuilt>();
        }

        var teams = await _teams.ListAsync(cancellationToken);

        // Nobody's club. A club somebody is running is that person's to spend, and a world that
        // improved a manager's ground behind his back would be a world where the button on his
        // own page could arrive at a ground he did not buy.
        var candidates = teams.Where(team => !team.IsManagerClub).ToList();

        if (candidates.Count == 0)
        {
            return Array.Empty<NpcStadiumBuilt>();
        }

        var clubIds = candidates.Select(team => team.Id).ToList();

        var grounds = (await _grounds.ListAsync(cancellationToken))
            .Where(ground => clubIds.Contains(ground.ClubId))
            .ToDictionary(ground => ground.ClubId);

        var open = (await _constructions.ListUnfinishedAsync(cancellationToken))
            .Where(construction => clubIds.Contains(construction.ClubId))
            .ToDictionary(construction => construction.ClubId);

        // What each club wanted to come, and whether anybody turned them away. A club absent from
        // this read is a club the world has never measured, and the policy has something to say
        // about that which is not "build" and not "do not build".
        var gates = await _matches.ReadHomeGatesAsync(seasonId, clubIds, cancellationToken);

        var crowds = await _fanBases.ListBySeasonIndexAsync(seasonId, cancellationToken);

        // The books. Read as the last line of each rather than as a sum, for the same reason
        // every other balance in this codebase is a last line and not a career total.
        var books = await _finance.GetLastForTeamsAsync(clubIds, cancellationToken);

        // Every project this season, finished or not. It is the guard against a day approving
        // the same thing twice, and it is one read because the question is asked of the whole
        // country rather than of each club in turn.
        var startedThisSeason = await _constructions.ListBySeasonAsync(seasonId, cancellationToken);

        // And one wage bill for the country, from one read of the contracts.
        var wages = SeasonWageBill(await _teams.ListAllContractsAsync(seasonId, cancellationToken));

        var approvedThisRound = startedThisSeason
            .Where(construction => construction.StartedAfterRound >= playedThroughRound)
            .Select(construction => construction.ClubId)
            .ToHashSet();

        var built = new List<NpcStadiumBuilt>();

        foreach (var club in candidates)
        {
            if (!grounds.TryGetValue(club.Id, out var ground))
            {
                continue;
            }

            // Already a building site. The friendly version of the fact the unique index on the
            // open project enforces, and the reason a double press costs nothing.
            if (open.ContainsKey(club.Id))
            {
                continue;
            }

            crowds.TryGetValue(club.Id, out var crowd);
            gates.TryGetValue(club.Id, out var gate);
            books.TryGetValue(club.Id, out var lastLine);

            var facts = new NpcStadiumFacts(
                club.Id,
                ground.Capacity,
                gate?.AverageDemand is { } demand ? (int)Math.Round(demand) : 0,
                gate?.AverageDemand is not null,
                crowd?.Grew ?? false,
                lastLine?.BalanceAfter ?? 0m,
                wages.GetValueOrDefault(club.Id, 0m));

            // The same day, twice. A club that approved a project when this round was already
            // played through has had its day; a second pass over it must not approve another.
            if (approvedThisRound.Contains(club.Id))
            {
                continue;
            }

            var decision = NpcStadiumPolicy.Decide(facts);

            if (!decision.Builds || decision.Project is not { } project)
            {
                continue;
            }

            var started = await _stadium
                .StartExpansionAsync(club.Id, project.Seats, playedThroughRound, cancellationToken);

            // The service may still have refused — a project that appeared between the read and
            // the write, which is the same race the index catches. Asking what happened beats
            // assuming, and a report that claimed a build that did not happen would be a report
            // about a building site nobody paid for.
            if (started.Kind != StadiumWorkKind.Started)
            {
                continue;
            }

            built.Add(new NpcStadiumBuilt(
                club.Id,
                started.Seats,
                started.Cost,
                started.FinishesAfterRound,
                decision.Verdict));
        }

        return built;
    }

    /// <summary>
    /// What every club's squad costs for the season.
    /// </summary>
    /// <remarks>
    /// A club with no contracts on the book is a club with no wage bill, which is a real state
    /// of a club being wound up and not a missing fact. The reserve is compared against zero
    /// for it, so it is held only by what it has.
    /// </remarks>
    public static IReadOnlyDictionary<Guid, decimal> SeasonWageBill(
        IReadOnlyList<TeamMembership> contracts) =>
        contracts
            .GroupBy(contract => contract.TeamId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(contract => contract.Wage));
}