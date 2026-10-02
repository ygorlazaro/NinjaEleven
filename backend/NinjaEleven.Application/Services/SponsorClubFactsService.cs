using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Sponsors;

namespace NinjaEleven.Application.Services;

/// <summary>
/// Everything a sponsor's price depends on, for a whole set of clubs at once.
///
/// <para>
/// It is a service rather than part of <see cref="SponsorOfferService"/> because two callers
/// want it — a manager reading his own book, and the pass that signs deals for the clubs
/// nobody manages — and because it is the part of the answer that costs the most: four
/// tables, the cup and every recent match of every club. Reading it per club is what would
/// make a world of sixty-four clubs a world that answers sixty-four questions sixty-four
/// times.
/// </para>
/// </summary>
public class SponsorClubFactsService
{
    private readonly ICompetitionRepository _competitions;
    private readonly IMatchRepository _matches;
    private readonly ICupTieRepository _cupTies;
    private readonly IStandingsReader _standings;

    public SponsorClubFactsService(
        ICompetitionRepository competitions,
        IMatchRepository matches,
        ICupTieRepository cupTies,
        IStandingsReader standings)
    {
        _competitions = competitions;
        _matches = matches;
        _cupTies = cupTies;
        _standings = standings;
    }

    /// <summary>
    /// Where every club of a season stands, which division it is in and how big that division
    /// is — the part of the answer that is the same for every sponsor and so is read once.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, ClubStanding>> ReadPlacementsAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        var placements = new Dictionary<Guid, ClubStanding>();

        var views = await _competitions.ListSeasonViewsAsync(seasonId, cancellationToken);

        foreach (var division in views.Where(view => view.IsDivision && view.Tier is not null))
        {
            var table = await _standings.GetAsync(division.Id, cancellationToken);
            var source = table.HasLiveMatches ? table.Projected : table.Official;

            var participants = await _competitions.ListParticipantsAsync(division.Id, cancellationToken);
            var clubsInDivision = source.Count;

            foreach (var participant in participants)
            {
                var row = source.FirstOrDefault(line => line.TeamId == participant.TeamId);

                placements[participant.TeamId] = new ClubStanding
                {
                    TeamId = participant.TeamId,
                    SeasonId = seasonId,
                    CompetitionSeasonId = division.Id,
                    DivisionName = division.Name,
                    Tier = division.Tier!.Value,
                    ClubsInDivision = clubsInDivision,
                    // The position is the table's own line, not a number counted here: a club
                    // with no line has not played, and it is not last — it is unplaced.
                    Row = row
                };
            }
        }

        return placements;
    }

    /// <summary>
    /// The whole of what a sponsor prices a club on, for every club of a season.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, ClubSponsorFacts>> ReadAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        var placements = await ReadPlacementsAsync(seasonId, cancellationToken);
        var history = await _matches.GetTeamHistoryByTeamsAsync(
            placements.Keys,
            SponsorPricing.FormMatches,
            cancellationToken);

        var aliveInTheCup = await AliveInTheCupAsync(seasonId, cancellationToken);

        // The average crowd of a division is measured from the same recent matches the club's
        // own crowd is: one read of the football answers both questions, and a division's
        // average crowd read from its own clubs is the crowd a sponsor's club is measured
        // against in any case.
        var divisionCrowd = DivisionAverageAttendance(placements, history);

        var facts = new Dictionary<Guid, ClubSponsorFacts>();

        foreach (var (teamId, placement) in placements)
        {
            var recent = history.TryGetValue(teamId, out var lines)
                ? lines
                : Array.Empty<TeamMatchRecord>();

            var league = recent.Where(line => line.IsDivision).ToList();
            var ownCrowd = recent
                .Where(line => line.IsHome && line.Attendance is not null)
                .Select(line => (double)line.Attendance!.Value)
                .ToList();

            facts[teamId] = new ClubSponsorFacts
            {
                Position = placement.Row?.Position,
                ClubsInDivision = placement.ClubsInDivision is { } clubs ? Math.Max(1, clubs) : 1,
                PointsPerGame = PointsPerGame(league),
                StillInCup = aliveInTheCup.Contains(teamId),
                ReachedCupFinal = false,
                AverageAttendance = ownCrowd.Count > 0 ? ownCrowd.Average() : null,
                // A division nobody has measured is not a division with a crowd of its own:
                // zero is the honest answer and the crowd factor reads zero as neutral, which
                // is what a missing average is.
                DivisionAverageAttendance = placement.Tier is { } tier
                                            && divisionCrowd.TryGetValue(tier, out var average)
                    ? average
                    : 0
            };
        }

        return facts;
    }

    /// <summary>
    /// Which clubs are still in the cup this season, which is the club's own cup exposure.
    /// </summary>
    private async Task<IReadOnlySet<Guid>> AliveInTheCupAsync(
        Guid seasonId,
        CancellationToken cancellationToken)
    {
        var alive = new HashSet<Guid>();

        var views = await _competitions.ListSeasonViewsAsync(seasonId, cancellationToken);

        foreach (var cup in views.Where(view => view.Type is CompetitionType.Cup))
        {
            foreach (var club in await _cupTies.GetAliveClubsInCupAsync(cup.Id, cancellationToken))
            {
                alive.Add(club);
            }
        }

        return alive;
    }

    private static double? PointsPerGame(IReadOnlyList<TeamMatchRecord> league)
    {
        if (league.Count == 0)
        {
            return null;
        }

        var points = league.Sum(line =>
            line.GoalsFor > line.GoalsAgainst ? 3 : line.GoalsFor == line.GoalsAgainst ? 1 : 0);

        return (double)points / league.Count;
    }

    private static Dictionary<int, double> DivisionAverageAttendance(
        IReadOnlyDictionary<Guid, ClubStanding> placements,
        IReadOnlyDictionary<Guid, IReadOnlyList<TeamMatchRecord>> history)
    {
        var byTier = new Dictionary<int, List<double>>();

        foreach (var (teamId, placement) in placements)
        {
            if (!history.TryGetValue(teamId, out var lines) || placement.Tier is not { } tier)
            {
                continue;
            }

            if (!byTier.TryGetValue(tier, out var crowds))
            {
                crowds = [];
                byTier[tier] = crowds;
            }

            crowds.AddRange(lines
                .Where(line => line.IsHome && line.Attendance is not null)
                .Select(line => (double)line.Attendance!.Value));
        }

        return byTier.ToDictionary(entry => entry.Key, entry => entry.Value.Average());
    }
}