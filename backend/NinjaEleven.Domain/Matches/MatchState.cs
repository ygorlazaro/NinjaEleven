
namespace NinjaEleven.Domain.Matches;

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

    /// <summary>
    /// Who came on and who went off before the interval, in the order it happened.
    ///
    /// It is kept because the second half opens by saying whether the two managers changed
    /// anything, and that sentence cannot be written from the counts alone: "o time fez uma
    /// troca" is not the same information as the name of the man who came on.
    /// </summary>
    public List<string> FirstHalfChanges { get; } = new();

    public int StoppageMinutes { get; set; }
    public int MatchSeconds => (90 + StoppageMinutes) * 60;

    /// <summary>
    /// The added time of each half. The total is drawn once, the way a referee announces
    /// it, and then split: the first half collects injuries and the second collects goals,
    /// so the two halves are not given the same minutes.
    /// </summary>
    public int FirstHalfStoppage { get; set; }

    public int SecondHalfStoppage { get; set; }

    /// <summary>
    /// When each half's added time has been announced. A manager is told the minutes
    /// while he still has to play them, not afterwards from the result.
    /// </summary>
    public bool FirstHalfStoppageAnnounced { get; set; }

    public bool SecondHalfStoppageAnnounced { get; set; }

    /// <summary>
    /// Ticks the clock stands still for. A goal, a red card and a serious injury all
    /// need a moment to be looked at; the hold is counted in ticks so it is the same
    /// stretch of the match whatever the speed the screen is running at.
    /// </summary>
    public int HoldTicksRemaining { get; set; }

    /// <summary>
    /// The shape each club is playing, read off the eleven that is on the pitch. It moves
    /// when a substitution changes the balance of the side, which is the point: replacing
    /// a striker for a defender does not just change two names.
    /// </summary>
    public Formation HomeFormation { get; set; } = Formation.Default;

    public Formation AwayFormation { get; set; } = Formation.Default;

    public bool MatchStarted { get; set; }
    public bool MatchFinished { get; set; }
    public bool HalfTimePauseActive { get; set; }
    public bool HalftimeShown { get; set; }
    public bool Paused { get; set; }
    public int Speed { get; set; } = 1;

    public int HomeShots { get; set; }
    public int AwayShots { get; set; }
    public int HomeShotsOnTarget { get; set; }
    public int AwayShotsOnTarget { get; set; }
    public int HomeCorners { get; set; }
    public int AwayCorners { get; set; }
    public int HomeCards { get; set; }
    public int AwayCards { get; set; }

    /// <summary>
    /// Saves made by each goalkeeper. It was a column in the statistics row that nothing
    /// ever wrote to, so a match where the keeper did the most important thing he could do
    /// reported nothing about it.
    /// </summary>
    public int HomeSaves { get; set; }
    public int AwaySaves { get; set; }
    public int HomeFouls { get; set; }
    public int AwayFouls { get; set; }

    /// <summary>
    /// How much of the match each side has had the ball, in seconds. The share on the
    /// screen is read off these and nothing else, so the number is a fact about the match
    /// rather than a counter that drifts away from it.
    /// </summary>
    public int HomePossessionSeconds { get; set; }

    public int AwayPossessionSeconds { get; set; }

    public int LastScoreHome { get; set; }
    public int LastScoreAway { get; set; }
    public bool[] WasEverBehind { get; set; } = new bool[2];
    public int EventHoldUntil { get; set; }

    public int PossessionTeam { get; set; }
    public Guid? PossessionPlayerId { get; set; }

    public bool PenaltyAwaitingSelection { get; set; }
    public int? PenaltyTeam { get; set; }

    /// <summary>
    /// The club the manager is watching, when the match was started by a manager. It is
    /// what tells the engine whose penalty taker he gets to choose.
    /// </summary>
    public Guid? ManagerTeamId { get; set; }

    public int InjuriesThisMatch { get; set; }

    /// <summary>
    /// Whether the taker of a penalty awarded to <paramref name="teamId"/> is a decision
    /// the manager makes. He decides for his own club only; the engine picks the taker of
    /// the other side, because nobody is watching that decision.
    /// </summary>
    public bool ManagerSelectsPenaltyTaker(Guid teamId) => ManagerTeamId == teamId;

    /// <summary>
    /// A player who cannot continue and whose club the manager is running, waiting for the
    /// manager to name who comes on. It stands the clock still in exactly the way a penalty
    /// waiting for its taker does, and it is cleared by the substitution itself.
    /// </summary>
    public bool InjuryAwaitingSubstitution { get; set; }

    /// <summary>Who is off the pitch until that substitution is made.</summary>
    public Guid? InjuryPlayerId { get; set; }

    /// <summary>Which of the two sides the injured player belongs to: 1 home, 2 away.</summary>
    public int? InjuryTeam { get; set; }

    /// <summary>
    /// How many matches the injury takes out of him, drawn the moment the knock happened.
    /// It is held here rather than on the player because the player is not marked as hurt
    /// until the change is made: he is still on the pitch until then, and a player who is
    /// not on the pitch cannot be the one a substitution is made for.
    /// </summary>
    public int PendingInjuryMatchesOut { get; set; }

    /// <summary>
    /// Whether a player of <paramref name="teamId"/> going off is the manager's decision.
    /// He decides for his own club only; the engine covers the other side itself, because
    /// nobody is watching that choice.
    /// </summary>
    public bool ManagerSelectsInjuryReplacement(Guid teamId) => ManagerTeamId == teamId;

    /// <summary>
    /// Settles an injury the manager was asked about, once he has named the replacement.
    /// The knock is applied here rather than when it happened, because the man was still
    /// playing until the change was made; a player who is not marked hurt stays off the
    /// match statistics as well, so a season absence is written for an injury the manager
    /// actually saw and acted on.
    /// </summary>
    public void ResolvePendingInjury(MatchPlayerSnapshot outgoing)
    {
        if (!InjuryAwaitingSubstitution || InjuryPlayerId != outgoing.PlayerId)
        {
            return;
        }

        outgoing.Injure(Common.Injury.Grave, PendingInjuryMatchesOut);

        InjuryAwaitingSubstitution = false;
        InjuryPlayerId = null;
        InjuryTeam = null;
        PendingInjuryMatchesOut = 0;
    }

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
        ManagerTeamId = context.ManagerTeamId;

        // Everybody who started the match has played it. From here on the flag is kept by
        // the substitutions, because the difference between a man who played and a man who
        // watched is what the recovery between matches is built on.
        foreach (var player in HomeLineup.Concat(AwayLineup))
        {
            player.PlayedInMatch = true;
        }
    }

    public int DisplayedHomeScore => HomeScore;
    public int DisplayedAwayScore => AwayScore;

    public int TotalPossessionSeconds => HomePossessionSeconds + AwayPossessionSeconds;

    /// <summary>
    /// The share of the ball each side has had, as a percentage, inside the band a real
    /// match stays in. A side that never sees the other half is not a football match, and
    /// the number on the screen has to be readable.
    /// </summary>
    public int HomePossession => PossessionShare(HomePossessionSeconds, AwayPossessionSeconds);

    public int AwayPossession => 100 - HomePossession;

    private static int PossessionShare(int home, int away)
    {
        if (home + away <= 0)
        {
            return 50;
        }

        var share = home * 100.0 / (home + away);

        return Math.Clamp((int)Math.Round(share), MatchRules.MinPossession, MatchRules.MaxPossession);
    }
}