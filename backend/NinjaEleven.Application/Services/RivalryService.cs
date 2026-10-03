using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Services;

/// <summary>
/// One rival, and the games that made it one.
/// </summary>
public record RivalLine(
    Guid OpponentTeamId,
    string OpponentName,
    int Meetings,
    int Wins,
    int Draws,
    int Defeats,
    int GoalsFor,
    int GoalsAgainst,
    double Score,
    RivalryGrowth Why);

/// <summary>
/// A club's rivals, worked out of the football it has actually played.
///
/// <para>
/// The whole club's history is read once and grouped here. Reading it an opponent at a time is
/// how a page of four rivals became eighty queries, and eighty queries is a number a manager
/// watches the screen think for.
///
/// <para>
/// The reading is over the club's <em>career</em> rather than its season, because a rivalry that
/// is rebuilt from scratch every August is not a rivalry — it is a form guide. The score is the
/// weighted reading in <see cref="RivalryRules"/>, and the four that come back are the four the
/// club has the strongest claim on.
/// </para>
/// </summary>
public class RivalryService
{
    /// <summary>
    /// How far back the reading goes. A club plays about sixty matches a season, so this is a
    /// career of several seasons rather than a page of the current one — and it is a bound rather
    /// than no bound because a reader with no limit is a reader whose cost grows without the
    /// screen's knowing it.
    /// </summary>
    public const int CareerHistoryLimit = 2_000;

    private readonly IMatchRepository _matches;
    private readonly ITeamRepository _teams;

    public RivalryService(IMatchRepository matches, ITeamRepository teams)
    {
        _matches = matches;
        _teams = teams;
    }

    /// <summary>
    /// The four clubs this one is most a rival of, most rival first.
    /// </summary>
    public async Task<IReadOnlyList<RivalLine>> RivalsOfAsync(
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        // One read of the club's own football, newest first. Everything below is a reading of
        // those rows rather than a question asked of the database again.
        var history = await _matches.GetTeamHistoryAsync(
            teamId,
            CareerHistoryLimit,
            cancellationToken);

        if (history.Count == 0)
        {
            return Array.Empty<RivalLine>();
        }

        var byOpponent = history
            .GroupBy(record => record.OpponentTeamId)
            .Select(group => Read(group.Key, group.ToList()))
            .Where(line => line.Meetings > 0)
            .OrderByDescending(line => line.Score)
            .ThenByDescending(line => line.Meetings)
            .ThenBy(line => line.OpponentName, StringComparer.Ordinal)
            .Take(RivalryRules.MaxRivals)
            .ToList();

        return byOpponent;
    }

    /// <summary>
    /// One opponent's whole relationship with the club, in one pass over that opponent's games.
    /// </summary>
    /// <remarks>
    /// The five bands are read here and carried on the line, so a screen can say what a rivalry
    /// is made of instead of only how big it is. That is the difference between "the number is
    /// 0.66" and "the number is 0.66 because you have met six times and split them".
    /// </remarks>
    private static RivalLine Read(Guid opponentTeamId, List<Models.TeamMatchRecord> records)
    {
        var ordered = records
            .OrderBy(record => record.PlayedAt)
            .Select(record => new RivalryFixture(
                record.PlayedAt,
                record.IsHome,
                record.GoalsFor,
                record.GoalsAgainst))
            .ToList();

        var recurrence = RivalryRules.Recurrence(ordered);
        var decisiveness = RivalryRules.Decisiveness(ordered);
        var results = RivalryRules.Results(ordered);
        var streaks = RivalryRules.Streaks(ordered);
        var recent = RivalryRules.RecentForm(ordered);

        return new RivalLine(
            opponentTeamId,
            records[0].OpponentName,
            records.Count,
            records.Count(record => record.GoalsFor > record.GoalsAgainst),
            records.Count(record => record.GoalsFor == record.GoalsAgainst),
            records.Count(record => record.GoalsFor < record.GoalsAgainst),
            records.Sum(record => record.GoalsFor),
            records.Sum(record => record.GoalsAgainst),
            RivalryRules.Score(ordered),
            new RivalryGrowth(
                recurrence,
                decisiveness,
                results,
                streaks,
                recent,
                RivalryRules.RecurrenceWeight * recurrence
                + RivalryRules.DecisivenessWeight * decisiveness
                + RivalryRules.ResultsWeight * results
                + RivalryRules.StreaksWeight * streaks
                + RivalryRules.RecentFormWeight * recent));
    }
}
