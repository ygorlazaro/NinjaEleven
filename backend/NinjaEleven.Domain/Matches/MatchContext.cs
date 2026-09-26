using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Players;

namespace NinjaEleven.Domain.Matches;

/// <summary>
/// Everything the match engine needs to run a match, plus nothing else.
/// This is intentionally independent of HTTP, SignalR, EF Core and PostgreSQL.
/// </summary>
public class MatchContext
{
    public Guid MatchId { get; }
    public TeamInfo HomeTeam { get; }
    public TeamInfo AwayTeam { get; }
    public List<MatchPlayerSnapshot> HomeLineup { get; }
    public List<MatchPlayerSnapshot> AwayLineup { get; }
    public List<MatchPlayerSnapshot> HomeBench { get; }
    public List<MatchPlayerSnapshot> AwayBench { get; }
    public IRandomSource Random { get; }

    /// <summary>
    /// The club a manager is watching, or <c>null</c> when the match is simulated without
    /// anybody in the dugout.
    /// </summary>
    public Guid? ManagerTeamId { get; }

    public MatchContext(
        Guid matchId,
        TeamInfo homeTeam,
        TeamInfo awayTeam,
        List<MatchPlayerSnapshot> homeLineup,
        List<MatchPlayerSnapshot> awayLineup,
        List<MatchPlayerSnapshot> homeBench,
        List<MatchPlayerSnapshot> awayBench,
        IRandomSource random,
        Guid? managerTeamId = null)
    {
        MatchId = matchId;
        HomeTeam = homeTeam ?? throw new ArgumentNullException(nameof(homeTeam));
        AwayTeam = awayTeam ?? throw new ArgumentNullException(nameof(awayTeam));
        HomeLineup = homeLineup ?? new List<MatchPlayerSnapshot>();
        AwayLineup = awayLineup ?? new List<MatchPlayerSnapshot>();
        HomeBench = homeBench ?? new List<MatchPlayerSnapshot>();
        AwayBench = awayBench ?? new List<MatchPlayerSnapshot>();
        Random = random ?? throw new ArgumentNullException(nameof(random));
        ManagerTeamId = managerTeamId;
    }
}