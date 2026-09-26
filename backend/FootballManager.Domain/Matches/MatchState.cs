
namespace FootballManager.Domain.Matches;

/// <summary>
/// Mutable runtime state of a match. This is the engine's working memory.
/// It is intentionally independent of persistence and transport.
/// </summary>
public class MatchState
{
    public Guid MatchId { get; }
    public TeamInfo HomeTeam { get; }
    public TeamInfo AwayTeam { get; }

    public List<MatchPlayerSnapshot> HomeLineup { get; }
    public List<MatchPlayerSnapshot> AwayLineup { get; }
    public List<MatchPlayerSnapshot> HomeBench { get; }
    public List<MatchPlayerSnapshot> AwayBench { get; }

    public int Minute { get; set; }
    public int GameSeconds { get; set; }
    public int Seconds => GameSeconds % 60;

    public int HomeScore { get; set; }
    public int AwayScore { get; set; }

    public int Half { get; set; } // 0 = first, 1 = second
    public int Sequence { get; set; }
    public int SubstitutionsHome { get; set; }
    public int SubstitutionsAway { get; set; }

    public int StoppageMinutes { get; set; }
    public int MatchSeconds => (90 + StoppageMinutes) * 60;

    public bool MatchStarted { get; set; }
    public bool MatchFinished { get; set; }
    public bool HalfTimePauseActive { get; set; }
    public bool HalftimeShown { get; set; }
    public bool Paused { get; set; }
    public bool HalfPaused { get; set; }
    public int Speed { get; set; } = 1;

    public int HomeShots { get; set; }
    public int AwayShots { get; set; }
    public int HomeShotsOnTarget { get; set; }
    public int AwayShotsOnTarget { get; set; }
    public int HomeCorners { get; set; }
    public int AwayCorners { get; set; }
    public int HomeCards { get; set; }
    public int AwayCards { get; set; }
    public int HomeFouls { get; set; }
    public int AwayFouls { get; set; }
    public int HomePossession { get; set; } = 50;

    public int LastScoreHome { get; set; }
    public int LastScoreAway { get; set; }
    public bool[] WasEverBehind { get; set; } = new bool[2];
    public int EventHoldUntil { get; set; }

    public int PossessionTeam { get; set; }
    public Guid? PossessionPlayerId { get; set; }

    public bool PenaltyAwaitingSelection { get; set; }
    public int? PenaltyTeam { get; set; }

    public List<MatchEngineEvent> PendingFeed { get; set; } = new();

    public MatchState(MatchContext context)
    {
        MatchId = context.MatchId;
        HomeTeam = context.HomeTeam;
        AwayTeam = context.AwayTeam;
        HomeLineup = context.HomeLineup;
        AwayLineup = context.AwayLineup;
        HomeBench = context.HomeBench;
        AwayBench = context.AwayBench;
    }

    public int DisplayedHomeScore => HomeScore;
    public int DisplayedAwayScore => AwayScore;

    public int AwayPossession => 100 - HomePossession;
}