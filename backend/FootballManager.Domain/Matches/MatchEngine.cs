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
                $"Bola rolando: {state.HomeTeam.Name} x {state.AwayTeam.Name}",
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

        // A penalty has to be taken before the clock moves on: whoever is waiting to pick
        // the taker is deciding the outcome of a goal, not of the next minute.
        if (state.PenaltyAwaitingSelection)
        {
            TakeAutomaticPenalty(state, events);
            state.PendingFeed.AddRange(events);
            return events;
        }

        // A club that lost its last goalkeeper promotes somebody at once, whatever
        // happened: the next action always finds a goal defended.
        EnsureGoalkeeper(state, events, home: true);
        EnsureGoalkeeper(state, events, home: false);

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
                $"Fim do primeiro tempo: {state.HomeScore} x {state.AwayScore}",
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
                $"Fim de jogo: {state.HomeScore} x {state.AwayScore}",
                "whistle"));
        }

        state.PendingFeed.AddRange(events);

        return events;
    }

    /// <summary>
    /// Takes a penalty nobody chose the taker for, which is the case of the club no
    /// manager is watching: the engine picks its best penalty taker and the shot is
    /// resolved.
    /// </summary>
    private void TakeAutomaticPenalty(MatchState state, List<MatchEngineEvent> events)
    {
        var home = state.PenaltyTeam is 1;
        var awardedTeamId = home ? state.HomeTeam.Id : state.AwayTeam.Id;

        // The manager of the awarded club names his own taker: the clock keeps waiting.
        if (state.ManagerSelectsPenaltyTaker(awardedTeamId))
        {
            return;
        }

        var lineup = home ? state.HomeLineup : state.AwayLineup;
        var taker = BestPenaltyTaker(lineup);

        if (taker is null)
        {
            state.PenaltyAwaitingSelection = false;
            state.PenaltyTeam = null;
            return;
        }

        events.AddRange(TakePenalty(state, home, taker));
    }

    /// <summary>
    /// The players a club could send to the spot, best first. Only the ones still on the
    /// pitch are eligible, and an outfield player is preferred over a goalkeeper when the
    /// numbers are close: a keeper is a poor taker, and a real manager knows it.
    /// </summary>
    public static IReadOnlyList<MatchPlayerSnapshot> PenaltyTakerCandidates(MatchState state, bool home)
    {
        var lineup = home ? state.HomeLineup : state.AwayLineup;

        return lineup
            .Where(player => player.IsOnPitch)
            .OrderByDescending(PenaltySkill)
            .ToList();
    }

    private static int PenaltySkill(MatchPlayerSnapshot player) =>
        player.Accuracy * 2 + player.Dribbling + player.Strength - (player.Position == Position.GK ? 25 : 0);

    private static MatchPlayerSnapshot? BestPenaltyTaker(List<MatchPlayerSnapshot> lineup) =>
        lineup.Where(player => player.IsOnPitch).OrderByDescending(PenaltySkill).FirstOrDefault();

    /// <summary>
    /// Takes the penalty the engine awarded. The taker is already chosen; this resolves
    /// the shot itself, because a goalkeeper's reflexes decide it and not the client.
    /// </summary>
    public IEnumerable<MatchEngineEvent> TakePenalty(
        MatchState state,
        bool home,
        MatchPlayerSnapshot taker)
    {
        var events = new List<MatchEngineEvent>();
        var defendingLineup = home ? state.AwayLineup : state.HomeLineup;
        var keeper = defendingLineup.FirstOrDefault(player => player.KeepsGoal);

        // Around 76% of penalties go in, a little more for a clinical taker and a little
        // less against a great goalkeeper.
        var conversion = 0.76
            + (taker.Accuracy + taker.Dribbling - 20) / 100.0
            - (keeper is null ? 0 : (keeper.Reflexes + keeper.GoalkeeperPower - 20) / 150.0);

        state.PenaltyAwaitingSelection = false;
        state.PenaltyTeam = null;

        if (_random.NextDouble() < conversion)
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

            taker.Goals++;
            taker.MatchGoals++;

            events.Add(Emit(
                state,
                MatchEventType.PenaltyTaken,
                home ? state.HomeTeam.Id : state.AwayTeam.Id,
                taker.PlayerId,
                $"{taker.Name} converte a pênalti",
                "penalty-goal"));
            return events;
        }

        events.Add(Emit(
            state,
            MatchEventType.PenaltyTaken,
            home ? state.HomeTeam.Id : state.AwayTeam.Id,
            taker.PlayerId,
            keeper is null
                ? $"{taker.Name} perde a pênalti"
                : $"{taker.Name} bate mal e {keeper.Name} defende",
            "penalty-miss"));

        if (keeper is not null)
        {
            events.Add(Emit(
                state,
                MatchEventType.PenaltySaved,
                home ? state.AwayTeam.Id : state.HomeTeam.Id,
                keeper.PlayerId,
                $"Defesa de {keeper.Name} no pênalti",
                "save"));
        }

        return events;
    }

    /// <summary>
    /// Resumes the match after the half-time pause.
    /// </summary>
    public void ContinueSecondHalf(MatchState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));

        state.Half = 1;
        state.HalfTimePauseActive = false;

        state.PendingFeed.Add(Emit(
            state,
            MatchEventType.SecondHalfStarted,
            null,
            null,
            "Começa o segundo tempo",
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
                $"{shooter.Name} finaliza para fora",
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
                    ? $"{shooter.Name} finaliza, mas não há goleiro para defender"
                    : $"{keeper.Name} defende o chute de {shooter.Name}",
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
        scorer.MatchGoals++;

        if (ownGoal)
        {
            events.Add(Emit(
                state,
                MatchEventType.OwnGoalScored,
                home ? state.HomeTeam.Id : state.AwayTeam.Id,
                scorer.PlayerId,
                $"Gol contra de {scorer.Name}",
                "own-goal"));
            return;
        }

        events.Add(Emit(
            state,
            MatchEventType.GoalScored,
            home ? state.HomeTeam.Id : state.AwayTeam.Id,
            scorer.PlayerId,
            $"GOL! {scorer.Name}",
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
            $"Escanteio para {(home ? state.HomeTeam.Name : state.AwayTeam.Name)}",
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
            $"Falta cometida por {offender.Name}",
            "foul"));

        // A foul inside the area is a penalty, whatever the referee thinks of the tackle.
        if (_random.NextDouble() < 0.10)
        {
            AwardPenalty(state, events, offendingTeamIsHome: home);
            return;
        }

        if (_random.NextDouble() >= 0.22)
        {
            return;
        }

        // A second yellow of the match is a red, and so is the one that comes with the
        // referee having seen enough.
        if (offender.MatchYellowCards >= 1 || _random.NextDouble() < 0.12)
        {
            ShowRedCard(state, events, home, offender, secondYellow: offender.MatchYellowCards >= 1);
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
            $"Cartão amarelo para {offender.Name}",
            "yellow-card"));
    }

    /// <summary>
    /// Sends a player off and, when his club has nobody left in goal, promotes the best
    /// outfielder to goalkeeper. A club that runs out of goalkeepers does not stop playing
    /// football, it just plays without one.
    /// </summary>
    private void ShowRedCard(
        MatchState state,
        List<MatchEngineEvent> events,
        bool home,
        MatchPlayerSnapshot offender,
        bool secondYellow)
    {
        offender.SendOff();

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
            secondYellow
                ? $"Segundo amarelo: {offender.Name} está expulso"
                : $"Cartão vermelho para {offender.Name}",
            "red-card"));

        EnsureGoalkeeper(state, events, home);
    }

    /// <summary>
    /// Makes sure the club still has someone keeping the goal after losing one, promoting
    /// the best outfielder still on the pitch when there is nobody else.
    /// </summary>
    private void EnsureGoalkeeper(MatchState state, List<MatchEngineEvent> events, bool home)
    {
        var lineup = home ? state.HomeLineup : state.AwayLineup;
        var teamName = home ? state.HomeTeam.Name : state.AwayTeam.Name;

        if (lineup.Any(player => player.KeepsGoal))
        {
            return;
        }

        var promoted = lineup
            .Where(player => player.IsOnPitch)
            .OrderByDescending(player => player.Reflexes + player.GoalkeeperPower)
            .FirstOrDefault();

        if (promoted is null)
        {
            return;
        }

        promoted.PromoteToGoalkeeper();

        events.Add(Emit(
            state,
            MatchEventType.KeeperPromoted,
            home ? state.HomeTeam.Id : state.AwayTeam.Id,
            promoted.PlayerId,
            $"{teamName} sem goleiro: {promoted.Name} assume a meta",
            "emergency-keeper"));
    }

    /// <summary>
    /// Awards a penalty. The manager of the club the match is being watched for names the
    /// taker; the engine does it for the other side, and the clock waits for the choice.
    /// </summary>
    private void AwardPenalty(MatchState state, List<MatchEngineEvent> events, bool offendingTeamIsHome)
    {
        var fouledTeamId = offendingTeamIsHome ? state.AwayTeam.Id : state.HomeTeam.Id;
        var fouledTeamName = offendingTeamIsHome ? state.AwayTeam.Name : state.HomeTeam.Name;

        events.Add(Emit(
            state,
            MatchEventType.PenaltyAwarded,
            fouledTeamId,
            null,
            $"Pênalti para {fouledTeamName}",
            "penalty"));

        if (state.ManagerSelectsPenaltyTaker(fouledTeamId))
        {
            state.PenaltyAwaitingSelection = true;
            state.PenaltyTeam = offendingTeamIsHome ? 2 : 1;
            return;
        }

        var lineup = offendingTeamIsHome ? state.AwayLineup : state.HomeLineup;
        var taker = BestPenaltyTaker(lineup);

        if (taker is null)
        {
            return;
        }

        events.AddRange(TakePenalty(state, home: !offendingTeamIsHome, taker));
    }

    private void SimulateInjury(MatchState state, List<MatchEngineEvent> events, bool home)
    {
        var player = PickPlayer(home ? state.HomeLineup : state.AwayLineup);

        if (player == null)
        {
            return;
        }

        // Most knocks are nothing more than a lost sprint; a few put a player out of the
        // match and, when they are a keeper, out of the next few as well.
        var severityRoll = _random.NextDouble();
        var isSerious = severityRoll < 0.12;
        var leavesPitch = isSerious || severityRoll < 0.45;

        player.ApplyFatigue(_random.Next(2, 8));

        var injury = isSerious ? Injury.Grave : Injury.Light;

        if (!leavesPitch)
        {
            events.Add(Emit(
                state,
                MatchEventType.PlayerInjured,
                home ? state.HomeTeam.Id : state.AwayTeam.Id,
                player.PlayerId,
                $"{player.Name} sente dores e continua em campo",
                "injury"));
            return;
        }

        player.Injure(injury);
        state.InjuriesThisMatch++;

        events.Add(Emit(
            state,
            MatchEventType.PlayerInjured,
            home ? state.HomeTeam.Id : state.AwayTeam.Id,
            player.PlayerId,
            isSerious
                ? $"{player.Name} se lesiona gravemente e deixa o campo"
                : $"{player.Name} se machuca e deixa o campo",
            "injury"));

        EnsureGoalkeeper(state, events, home);
    }

    /// <summary>
    /// Picks a player who is actually on the pitch: sent off or injured, he is not an
    /// option for a new action.
    /// </summary>
    private MatchPlayerSnapshot? PickPlayer(List<MatchPlayerSnapshot> lineup)
    {
        var available = lineup.Where(p => p.IsOnPitch).ToList();

        if (available.Count == 0)
        {
            return null;
        }

        return available[_random.Next(0, available.Count)];
    }

    private static MatchPlayerSnapshot? FindGoalkeeper(List<MatchPlayerSnapshot> lineup)
    {
        return lineup.FirstOrDefault(p => p.KeepsGoal);
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
