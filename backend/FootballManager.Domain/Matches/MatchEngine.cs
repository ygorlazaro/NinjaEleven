using FootballManager.Domain.Common;
using FootballManager.Domain.Enums;

namespace FootballManager.Domain.Matches;

/// <summary>
/// Deterministic match engine. Every roll goes through the injected
/// <see cref="IRandomSource"/>, so the same seed always replays the same match.
/// The engine owns the clock and mutates <see cref="MatchState"/>; it never
/// touches persistence, HTTP or SignalR.
/// </summary>
public class MatchEngine
{
    private const int SecondsPerTick = 60;
    private const int FirstHalfEnd = 45;
    private const int MinStoppage = 2;
    private const int MaxStoppage = 5;

    private const int MinPossession = 30;
    private const int MaxPossession = 70;

    private readonly IRandomSource _random;

    public MatchEngine(IRandomSource random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    /// <summary>
    /// Starts a match at the given minute, draws the stoppage time and emits the kick-off.
    /// </summary>
    public IEnumerable<MatchEngineEvent> Initialize(MatchState state, int minute)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        if (state.MatchFinished) throw new InvalidOperationException("A finished match cannot be initialized.");

        state.MatchStarted = true;
        state.MatchFinished = false;
        state.Half = 0;
        state.Minute = minute;
        state.GameSeconds = minute * SecondsPerTick;
        state.StoppageMinutes = _random.Next(MinStoppage, MaxStoppage);
        state.HomePossession = 50;
        state.PossessionTeam = 0;

        return new[]
        {
            Emit(
                state,
                MatchEventType.KickOff,
                null,
                null,
                $"Kick-off: {state.HomeTeam.Name} vs {state.AwayTeam.Name}",
                "whistle")
        };
    }

    /// <summary>
    /// Advances the match by one tick and returns the events produced by it.
    /// The same events are also appended to <see cref="MatchState.PendingFeed"/>.
    /// </summary>
    public IEnumerable<MatchEngineEvent> Tick(MatchState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));

        var events = new List<MatchEngineEvent>();

        if (!state.MatchStarted || state.MatchFinished || state.HalfTimePauseActive)
        {
            return events;
        }

        state.GameSeconds += SecondsPerTick;
        state.Minute = state.GameSeconds / SecondsPerTick;

        SimulatePossession(state);
        SimulateAction(state, events);

        if (state.Half == 0 && state.Minute >= FirstHalfEnd)
        {
            state.HalfTimePauseActive = true;
            state.HalftimeShown = true;

            events.Add(Emit(
                state,
                MatchEventType.HalfTimeReached,
                null,
                null,
                $"Half-time: {state.HomeScore} - {state.AwayScore}",
                "whistle"));
        }
        else if (state.GameSeconds >= state.MatchSeconds)
        {
            state.MatchFinished = true;

            events.Add(Emit(
                state,
                MatchEventType.MatchFinished,
                null,
                null,
                $"Full-time: {state.HomeScore} - {state.AwayScore}",
                "whistle"));
        }

        state.PendingFeed.AddRange(events);

        return events;
    }

    /// <summary>
    /// Resumes the match after the half-time pause.
    /// </summary>
    public void ContinueSecondHalf(MatchState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));

        state.Half = 1;
        state.HalfPaused = false;
        state.HalfTimePauseActive = false;

        state.PendingFeed.Add(Emit(
            state,
            MatchEventType.SecondHalfStarted,
            null,
            null,
            "Second half started",
            "whistle"));
    }

    private void SimulatePossession(MatchState state)
    {
        var drift = _random.Next(-4, 4);
        var possession = state.HomePossession + drift;

        if (possession < MinPossession) possession = MinPossession;
        if (possession > MaxPossession) possession = MaxPossession;

        state.HomePossession = possession;
        state.PossessionTeam = possession >= 50 ? 0 : 1;
    }

    private void SimulateAction(MatchState state, List<MatchEngineEvent> events)
    {
        if (_random.NextDouble() >= 0.65)
        {
            return;
        }

        var roll = _random.NextDouble();
        var attackingHome = _random.NextDouble() < 0.55;

        if (roll < 0.40)
        {
            SimulateShot(state, events, attackingHome);
        }
        else if (roll < 0.58)
        {
            SimulateFoul(state, events, attackingHome);
        }
        else if (roll < 0.68)
        {
            SimulateCorner(state, events, attackingHome);
        }
        else if (roll < 0.72)
        {
            SimulateInjury(state, events, attackingHome);
        }
    }

    private void SimulateShot(MatchState state, List<MatchEngineEvent> events, bool home)
    {
        var lineup = home ? state.HomeLineup : state.AwayLineup;
        var shooter = PickPlayer(lineup);

        if (shooter == null)
        {
            return;
        }

        if (home)
        {
            state.HomeShots++;
        }
        else
        {
            state.AwayShots++;
        }

        var onTargetChance = 0.20 + shooter.Accuracy / 200.0;
        if (_random.NextDouble() >= onTargetChance)
        {
            events.Add(Emit(
                state,
                MatchEventType.Shot,
                home ? state.HomeTeam.Id : state.AwayTeam.Id,
                shooter.PlayerId,
                $"{shooter.Name} shoots off target",
                "shot"));
            return;
        }

        if (home)
        {
            state.HomeShotsOnTarget++;
        }
        else
        {
            state.AwayShotsOnTarget++;
        }

        var defendingLineup = home ? state.AwayLineup : state.HomeLineup;
        var keeper = FindGoalkeeper(defendingLineup);

        var saveChance = keeper == null ? 0.30 : 0.45 + (keeper.Reflexes + keeper.GoalkeeperPower) / 200.0;

        if (_random.NextDouble() < saveChance)
        {
            events.Add(Emit(
                state,
                MatchEventType.Save,
                keeper == null ? null : home ? state.AwayTeam.Id : state.HomeTeam.Id,
                keeper?.PlayerId,
                keeper == null
                    ? $"{shooter.Name} shoots, but there is no keeper to stop him"
                    : $"{keeper.Name} saves from {shooter.Name}",
                "save"));
            return;
        }

        if (_random.NextDouble() < 0.06)
        {
            ScoreGoal(state, events, home, shooter, ownGoal: true);
            return;
        }

        ScoreGoal(state, events, home, shooter, ownGoal: false);
    }

    private void ScoreGoal(
        MatchState state,
        List<MatchEngineEvent> events,
        bool home,
        MatchPlayerSnapshot scorer,
        bool ownGoal)
    {
        if (home)
        {
            state.HomeScore++;
            state.LastScoreHome = state.Minute;
        }
        else
        {
            state.AwayScore++;
            state.LastScoreAway = state.Minute;
        }

        scorer.Goals++;

        if (ownGoal)
        {
            events.Add(Emit(
                state,
                MatchEventType.OwnGoalScored,
                home ? state.HomeTeam.Id : state.AwayTeam.Id,
                scorer.PlayerId,
                $"Own goal by {scorer.Name}",
                "own-goal"));
            return;
        }

        events.Add(Emit(
            state,
            MatchEventType.GoalScored,
            home ? state.HomeTeam.Id : state.AwayTeam.Id,
            scorer.PlayerId,
            $"GOAL! {scorer.Name}",
            "goal"));
    }

    private void SimulateCorner(MatchState state, List<MatchEngineEvent> events, bool home)
    {
        var lineup = home ? state.HomeLineup : state.AwayLineup;
        var taker = PickPlayer(lineup);

        if (taker == null)
        {
            return;
        }

        if (home)
        {
            state.HomeCorners++;
        }
        else
        {
            state.AwayCorners++;
        }

        events.Add(Emit(
            state,
            MatchEventType.Corner,
            home ? state.HomeTeam.Id : state.AwayTeam.Id,
            taker.PlayerId,
            $"Corner for {(home ? state.HomeTeam.Name : state.AwayTeam.Name)}",
            "corner"));

        if (_random.NextDouble() < 0.25)
        {
            SimulateShot(state, events, home);
        }
    }

    private void SimulateFoul(MatchState state, List<MatchEngineEvent> events, bool home)
    {
        var offender = PickPlayer(home ? state.HomeLineup : state.AwayLineup);

        if (offender == null)
        {
            return;
        }

        if (home)
        {
            state.HomeFouls++;
        }
        else
        {
            state.AwayFouls++;
        }

        events.Add(Emit(
            state,
            MatchEventType.Foul,
            home ? state.HomeTeam.Id : state.AwayTeam.Id,
            offender.PlayerId,
            $"Foul by {offender.Name}",
            "foul"));

        if (_random.NextDouble() >= 0.22)
        {
            return;
        }

        if (offender.MatchYellowCards >= 1 || _random.NextDouble() < 0.12)
        {
            offender.RedCard = true;

            if (home)
            {
                state.HomeCards++;
            }
            else
            {
                state.AwayCards++;
            }

            events.Add(Emit(
                state,
                MatchEventType.RedCardShown,
                home ? state.HomeTeam.Id : state.AwayTeam.Id,
                offender.PlayerId,
                $"Red card for {offender.Name}",
                "red-card"));
            return;
        }

        offender.MatchYellowCards++;

        if (home)
        {
            state.HomeCards++;
        }
        else
        {
            state.AwayCards++;
        }

        events.Add(Emit(
            state,
            MatchEventType.YellowCardShown,
            home ? state.HomeTeam.Id : state.AwayTeam.Id,
            offender.PlayerId,
            $"Yellow card for {offender.Name}",
            "yellow-card"));
    }

    private void SimulateInjury(MatchState state, List<MatchEngineEvent> events, bool home)
    {
        var player = PickPlayer(home ? state.HomeLineup : state.AwayLineup);

        if (player == null)
        {
            return;
        }

        player.ApplyFatigue(_random.Next(2, 8));

        events.Add(Emit(
            state,
            MatchEventType.PlayerInjured,
            home ? state.HomeTeam.Id : state.AwayTeam.Id,
            player.PlayerId,
            $"{player.Name} is struggling",
            "injury"));
    }

    private MatchPlayerSnapshot? PickPlayer(List<MatchPlayerSnapshot> lineup)
    {
        var available = lineup.Where(p => !p.RedCard).ToList();

        if (available.Count == 0)
        {
            return null;
        }

        return available[_random.Next(0, available.Count - 1)];
    }

    private static MatchPlayerSnapshot? FindGoalkeeper(List<MatchPlayerSnapshot> lineup)
    {
        return lineup.FirstOrDefault(p => p.Position == Position.GK && !p.RedCard);
    }

    private static MatchEngineEvent Emit(
        MatchState state,
        MatchEventType type,
        Guid? teamId,
        Guid? playerId,
        string description,
        string icon)
    {
        state.Sequence++;

        return new MatchEngineEvent(
            state.Sequence,
            state.Minute,
            type,
            teamId,
            playerId,
            state.HomeScore,
            state.AwayScore,
            description,
            icon);
    }
}
