using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Domain.Matches;

/// <summary>
/// Deterministic match engine. Every roll goes through the injected
/// <see cref="IRandomSource"/>, so the same seed always replays the same match.
/// The engine owns the clock and mutates <see cref="MatchState"/>; it never
/// touches persistence, HTTP or SignalR.
///
/// The match is built out of a few rules that all read each other:
///
/// - a tick is half a minute of football, and most ticks are quiet;
/// - who has the ball, how well a side plays its way up the pitch and how likely a shot is
///   to be on target all come from <see cref="TeamStrength"/>, which reads the eleven that
///   is actually on the pitch and the shape it is playing in;
/// - running the match costs every player energy, and that energy is what the strength
///   above is made of, so a tiring side gets weaker as the game goes on;
/// - the club nobody is watching is played by the engine, with the substitutions a real
///   manager would make;
/// - and the eleven is read from positions, so the shape of a side is a consequence of who
///   is picked rather than a string nobody looks at.
///
/// The tuning numbers live in <see cref="MatchRules"/>, not scattered through the loops
/// that use them.
/// </summary>
public class MatchEngine
{
    /// <summary>
    /// How often a penalty goes in when an average taker faces an average goalkeeper.
    /// </summary>
    private const double BasePenaltyConversion = 0.78;

    /// <summary>
    /// The band no penalty can leave. A great taker is never certain and a poor one is
    /// never hopeless, so the engine never produces the absurd ratios these two bounds
    /// are there to prevent.
    /// </summary>
    private const double MinPenaltyConversion = 0.60;
    private const double MaxPenaltyConversion = 0.92;

    /// <summary>
    /// How far the whole conversion moves with a perfect taker and with an unwatchable
    /// goalkeeper. A penalty is decided more by the man at the spot than by the one in
    /// goal, which is why the two weights are not the same number.
    /// </summary>
    private const double ShootingWeight = 0.12;
    private const double SavingWeight = 0.10;

    private readonly IRandomSource _random;

    public MatchEngine(IRandomSource random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    /// <summary>
    /// Starts a match at the given minute, draws the added time and emits the kick-off.
    /// </summary>
    public IEnumerable<MatchEngineEvent> Initialize(MatchState state, int minute)
    {
        if (state is null) throw new ArgumentNullException(nameof(state));
        if (state.MatchFinished) throw new InvalidOperationException("A finished match cannot be initialized.");

        state.MatchStarted = true;
        state.MatchFinished = false;
        state.Half = 0;
        state.Minute = minute;
        state.GameSeconds = minute * 60;
        state.StoppageMinutes = _random.Next(MatchRules.MinStoppage, MatchRules.MaxStoppage);

        // The referee announces one number for the match and divides it between the halves
        // when he gets there: the first half collects injuries, the second collects goals.
        state.FirstHalfStoppage = (int)Math.Round(state.StoppageMinutes * MatchRules.FirstHalfStoppageShare);
        state.SecondHalfStoppage = state.StoppageMinutes - state.FirstHalfStoppage;
        state.FirstHalfStoppageAnnounced = false;
        state.SecondHalfStoppageAnnounced = false;
        state.HoldTicksRemaining = 0;

        state.HomeFormation = Formation.FromComposition(state.HomeLineup);
        state.AwayFormation = Formation.FromComposition(state.AwayLineup);

        // The ball starts with the side that kicks it off, and neither has had a second of
        // it yet, so possession is an even split until something happens.
        state.HomePossessionSeconds = 0;
        state.AwayPossessionSeconds = 0;
        state.PossessionTeam = 0;
        state.PossessionPlayerId = FirstOf(state.HomeLineup)?.PlayerId;

        return new[]
        {
            // The eleven is said before the whistle, not after. A manager picks these ten
            // names, and a match that starts without saying who is playing hides the only
            // decision he made before anyone watched a minute of it.
            Emit(
                state,
                MatchEventType.LineupAnnounced,
                state.HomeTeam.Id,
                null,
                MatchNarration.Say(
                    _random,
                    MatchNarration.Lineup,
                    state.HomeTeam.Name,
                    DescribeEleven(state.HomeLineup)),
                "lineup"),
            Emit(
                state,
                MatchEventType.LineupAnnounced,
                state.AwayTeam.Id,
                null,
                MatchNarration.Say(
                    _random,
                    MatchNarration.Lineup,
                    state.AwayTeam.Name,
                    DescribeEleven(state.AwayLineup)),
                "lineup"),
            Emit(
                state,
                MatchEventType.KickOff,
                null,
                null,
                MatchNarration.Say(
                    _random,
                    MatchNarration.KickOff,
                    state.HomeTeam.Name),
                "whistle")
        };
    }

    /// <summary>
    /// Advances the match by one tick and returns the events produced by it.
    /// The same events are also appended to <see cref="MatchState.PendingFeed"/>.
    /// </summary>
    public IEnumerable<MatchEngineEvent> Tick(MatchState state)
    {
        if (state is null) throw new ArgumentNullException(nameof(state));

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

        // A goal, a red card and a serious injury all need a moment before the next half
        // minute. The clock waits; the rest of the match is unaffected.
        if (state.HoldTicksRemaining > 0)
        {
            state.HoldTicksRemaining--;
            return events;
        }

        // A club that lost its last goalkeeper promotes somebody at once, whatever
        // happened: the next action always finds a goal defended.
        EnsureGoalkeeper(state, events, home: true);
        EnsureGoalkeeper(state, events, home: false);

        state.GameSeconds += MatchRules.SecondsPerTick;
        state.Minute = state.GameSeconds / 60;

        AnnounceSecondHalfStoppage(state, events);

        if (_random.NextDouble() < MatchRules.ActionChancePerTick)
        {
            SimulateAction(state, events);
        }

        SubstituteForTheUnwatchedClub(state, events);

        DrainEnergy(state);
        AccountPossession(state);

        if (state.Half == 0 && !state.FirstHalfStoppageAnnounced && state.Minute >= MatchRules.FirstHalfStoppageAnnounceMinute)
        {
            state.FirstHalfStoppageAnnounced = true;

            // Announced at 41' rather than at the whistle: the half is still being played,
            // the number is a warning, and a manager who hears it at 45' has been told
            // something that has already happened.
            events.Add(Emit(
                state,
                MatchEventType.StoppageTimeAdded,
                null,
                null,
                MatchNarration.Say(
                    _random,
                    MatchNarration.StoppageFirst,
                    state.FirstHalfStoppage),
                "whistle"));
        }

        if (state.Half == 0 && state.GameSeconds >= MatchRules.FirstHalfEnd * 60)
        {
            state.HalfTimePauseActive = true;
            state.HalftimeShown = true;

            events.Add(Emit(
                state,
                MatchEventType.HalfTimeReached,
                null,
                null,
                MatchNarration.Say(
                    _random,
                    MatchNarration.HalfTime,
                    $"{state.HomeScore} x {state.AwayScore}",
                    state.AwayTeam.Name),
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
                MatchNarration.Say(
                    _random,
                    MatchNarration.FullTime,
                    $"{state.HomeScore} x {state.AwayScore}"),
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
        var taker = BestPenaltyTaker(lineup, GoalkeeperOf(home ? state.AwayLineup : state.HomeLineup));

        if (taker is null)
        {
            state.PenaltyAwaitingSelection = false;
            state.PenaltyTeam = null;
            return;
        }

        events.AddRange(TakePenalty(state, home, taker));
    }

    /// <summary>
    /// The players a club could send to the spot, best chance first. Only the ones still
    /// on the pitch are eligible, and the order is the very chance the engine would roll
    /// for each of them, so the first card of the dialog is the best one available: a
    /// keeper is a poor taker, and a real manager knows it.
    /// </summary>
    public static IReadOnlyList<MatchPlayerSnapshot> PenaltyTakerCandidates(MatchState state, bool home)
    {
        var lineup = home ? state.HomeLineup : state.AwayLineup;
        var keeper = GoalkeeperOf(home ? state.AwayLineup : state.HomeLineup);

        return lineup
            .Where(player => player.IsOnPitch)
            .OrderByDescending(player => PenaltyConversion(player, keeper))
            .ToList();
    }

    private static MatchPlayerSnapshot? BestPenaltyTaker(
        List<MatchPlayerSnapshot> lineup,
        MatchPlayerSnapshot? keeper) =>
        lineup
            .Where(player => player.IsOnPitch)
            .OrderByDescending(player => PenaltyConversion(player, keeper))
            .FirstOrDefault();

    private static MatchPlayerSnapshot? GoalkeeperOf(List<MatchPlayerSnapshot> lineup) =>
        lineup.FirstOrDefault(player => player.KeepsGoal);

    /// <summary>
    /// The chance a penalty goes in, as a fraction between 0 and 1. This is the one place
    /// the answer lives: the dialog shows it, the candidate list is sorted by it and the
    /// roll uses it, so what a manager is told and what the engine does cannot drift apart.
    ///
    /// A penalty is not a coin flip, but it is not a skill shot either. The reference is
    /// <see cref="BasePenaltyConversion"/>: an average taker against an average goalkeeper
    /// scores about three penalties in four. Finishing and control push it up, the
    /// goalkeeper's reflexes and power push it down, and the result is clamped to
    /// <see cref="MinPenaltyConversion"/>–<see cref="MaxPenaltyConversion"/> so no
    /// attribute combination can ever produce an absurd ratio.
    /// </summary>
    public static double PenaltyConversion(MatchPlayerSnapshot taker, MatchPlayerSnapshot? keeper)
    {
        if (taker is null) throw new ArgumentNullException(nameof(taker));

        // Finishing and control decide a penalty; the goalkeeper's reflexes and power are
        // what keep it out.
        var shooting = SkillFactor((taker.Accuracy + taker.Dribbling) / 2.0);
        var saving = keeper is null ? 0.0 : SkillFactor((keeper.Reflexes + keeper.GoalkeeperPower) / 2.0);

        var conversion = BasePenaltyConversion + ShootingWeight * shooting - SavingWeight * saving;

        return Math.Clamp(conversion, MinPenaltyConversion, MaxPenaltyConversion);
    }

    /// <summary>
    /// Places an averaged attribute on a -1..1 scale: 0 for a player of average quality on
    /// the 1..20 attribute scale, positive above it, negative below.
    /// </summary>
    private static double SkillFactor(double average) =>
        Math.Clamp((average - MatchRules.ReferenceAttribute) / MatchRules.AttributeSpan, -1.0, 1.0);

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
        var keeper = GoalkeeperOf(defendingLineup);

        // The very chance the dialog showed the manager, so the number he was given is the
        // number that is rolled.
        var conversion = PenaltyConversion(taker, keeper);

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
                MatchNarration.Say(
                    _random,
                    MatchNarration.PenaltyScored,
                    taker.Name),
                "penalty-goal"));

            // A converted penalty is a goal like any other, and everything that follows a
            // goal follows this one: the feed says GOL, the scorers count it, and whoever is
            // watching the match hears it. Without this the penalty was the one goal the
            // rest of the game never learned about.
            events.Add(Emit(
                state,
                MatchEventType.GoalScored,
                home ? state.HomeTeam.Id : state.AwayTeam.Id,
                taker.PlayerId,
                MatchNarration.Say(
                    _random,
                    MatchNarration.PenaltyScored,
                    taker.Name),
                "goal"));

            Hold(state);

            return events;
        }

        events.Add(Emit(
            state,
            MatchEventType.PenaltyTaken,
            home ? state.HomeTeam.Id : state.AwayTeam.Id,
            taker.PlayerId,
            MatchNarration.Say(
                _random,
                MatchNarration.PenaltyMissed,
                taker.Name,
                keeper?.Name ?? "o goleiro"),
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

        Hold(state);

        return events;
    }

    /// <summary>
    /// Resumes the match after the half-time pause.
    /// </summary>
    public void ContinueSecondHalf(MatchState state)
    {
        if (state is null) throw new ArgumentNullException(nameof(state));

        state.Half = 1;
        state.HalfTimePauseActive = false;
        state.HoldTicksRemaining = 0;

        state.PendingFeed.Add(Emit(
            state,
            MatchEventType.SecondHalfStarted,
            null,
            null,
            ChangesAtHalfTime(state),
            "whistle"));
    }

    /// <summary>
    /// What the second half opens with: whether either manager changed anything at the
    /// interval, and if so who for whom.
    ///
    /// It is said on the whistle of the second half rather than at the whistle of the first
    /// one, because that is when the change means something: a substitution announced at
    /// 45' is a decision, and the same substitution announced at 46' is the team the
    /// manager is about to watch.
    /// </summary>
    private string ChangesAtHalfTime(MatchState state)
    {
        if (state.FirstHalfChanges.Count == 0)
        {
            return MatchNarration.Say(_random, MatchNarration.HalftimeNoChanges);
        }

        return MatchNarration.Say(
            _random,
            MatchNarration.HalftimeChanges,
            string.Join(" | ", state.FirstHalfChanges));
    }

    // --- The clock --------------------------------------------------------------

    /// <summary>
    /// Tells the manager how long the second half will run, at the moment the first half's
    /// added time has been played out — the same point in the match at which a referee
    /// announces it, and while there is still time left to play it.
    /// </summary>
    private void AnnounceSecondHalfStoppage(MatchState state, List<MatchEngineEvent> events)
    {
        if (state.Half != 1 || state.SecondHalfStoppageAnnounced)
        {
            return;
        }

        if (state.Minute < MatchRules.SecondHalfStoppageAnnounceMinute)
        {
            return;
        }

        state.SecondHalfStoppageAnnounced = true;

        events.Add(Emit(
            state,
            MatchEventType.StoppageTimeAdded,
            null,
            null,
            MatchNarration.Say(
                _random,
                MatchNarration.StoppageSecond,
                state.SecondHalfStoppage),
            "whistle"));
    }

    /// <summary>
    /// What a stretch of the match costs every player on the pitch. The cost is his age
    /// and any knock he is playing through, and one tick in fifteen is a sprint: football
    /// is not a metronome, and a side whose energy only ever falls by the same amount is a
    /// spreadsheet.
    /// </summary>
    private void DrainEnergy(MatchState state)
    {
        foreach (var player in state.HomeLineup.Concat(state.AwayLineup))
        {
            if (!player.IsOnPitch)
            {
                continue;
            }

            var cost = MatchRules.EnergyCostPerTick
                * PlayerMetric.AgeCost(player)
                * PlayerMetric.InjuryCost(player);

            if (_random.NextDouble() < MatchRules.SprintChancePerTick)
            {
                cost *= 2;
            }

            player.DrainEnergy(cost, MatchRules.EnergyFloorDuringMatch);
        }
    }

    /// <summary>
    /// Gives the half minute of football just played to the side that had the ball. The
    /// share on the screen is read off these two numbers and nothing else, so it is a fact
    /// about the match rather than a counter that drifts away from it.
    /// </summary>
    private static void AccountPossession(MatchState state)
    {
        if (state.PossessionTeam == 1)
        {
            state.AwayPossessionSeconds += MatchRules.SecondsPerTick;
        }
        else
        {
            state.HomePossessionSeconds += MatchRules.SecondsPerTick;
        }
    }

    /// <summary>
    /// Stops the clock for a moment after something the crowd needs to look at.
    /// </summary>
    private static void Hold(MatchState state) =>
        state.HoldTicksRemaining = MatchRules.TicksHeldAfterMajorEvent;

    /// <summary>
    /// Puts the ball in the hands of a named player, which is what the eleven cards under
    /// the scoreboard are showing: not who is playing, but who has it.
    /// </summary>
    private static void SetPossession(MatchState state, bool home, MatchPlayerSnapshot? player)
    {
        if (player is null)
        {
            return;
        }

        state.PossessionTeam = home ? 0 : 1;
        state.PossessionPlayerId = player.PlayerId;
    }

    // --- The action -------------------------------------------------------------

    /// <summary>
    /// One thing that happens in a match. The side on the ball is chosen by what the two
    /// elevens are worth, not by a coin: the side that controls the game plays more of it,
    /// and the side with the better forwards converts more of what it does.
    /// </summary>
    private void SimulateAction(MatchState state, List<MatchEngineEvent> events)
    {
        var homeStrength = TeamStrength.Of(state.HomeLineup);
        var awayStrength = TeamStrength.Of(state.AwayLineup);

        var home = WinsTheBall(homeStrength, awayStrength);

        var attacking = home ? state.HomeLineup : state.AwayLineup;
        var defending = home ? state.AwayLineup : state.HomeLineup;
        var attackStrength = home ? homeStrength.Attack : awayStrength.Attack;
        var opponentsAttack = home ? awayStrength.Attack : homeStrength.Attack;

        var carriers = attacking.Where(player => player.IsOnPitch).ToList();
        if (carriers.Count == 0)
        {
            return;
        }

        SetPossession(state, home, carriers[_random.Next(0, carriers.Count)]);

        // A knock is more likely where there are more men to be knocked, and it is the
        // first thing that happens: the tackle comes before the pass.
        var injuryPool = Vulnerable(attacking);
        var injuryChance = injuryPool.Sum(PlayerMetric.InjuryChance);

        if (_random.NextDouble() < injuryChance)
        {
            var injured = injuryPool[_random.Next(0, injuryPool.Count)];
            TriggerInjury(state, events, home, injured);
            return;
        }

        if (_random.NextDouble() < MatchRules.AttackSequenceChance)
        {
            SimulateAttackSequence(state, events, home);
            return;
        }

        var roll = _random.NextDouble();

        if (roll < MatchRules.YellowCardLimit)
        {
            ShowYellowCard(state, events, home);
        }
        else if (roll < MatchRules.DirectRedCardLimit)
        {
            ShowDirectRedCard(state, events, home);
        }
        else if (roll < MatchRules.FoulLimit)
        {
            SimulateFoul(state, events, home);
        }
        else if (roll < MatchRules.CornerLimit)
        {
            SimulateCorner(state, events, home);
        }
        else if (roll < MatchRules.CornerLimit + MatchRules.OwnGoalFromClearance)
        {
            // The beat that is nobody's doing: the defending side is playing the ball out and
            // puts it through its own goal. It is a branch of the defensive half of the
            // action table, so it only happens when the defending side is the one acting.
            SimulateOwnGoal(state, events, !home);
        }
        else if (roll < MatchRules.ShotLimit)
        {
            SimulateOpenPlayShot(state, events, home, attackStrength, opponentsAttack);
        }
        else
        {
            SimulateDuel(state, events, home);
        }
    }

    /// <summary>
    /// Which side starts the action. Initiative is midfield weighted heavily and attack on
    /// top of it; a side with nothing on the pitch cannot win anything, and one team
    /// playing on its own gets the ball every time rather than by accident.
    /// </summary>
    private bool WinsTheBall(TeamStrength home, TeamStrength away)
    {
        var homeInitiative = home.Initiative;
        var awayInitiative = away.Initiative;
        var total = homeInitiative + awayInitiative;

        if (total <= 0)
        {
            return _random.NextDouble() < 0.5;
        }

        return _random.NextDouble() * total < homeInitiative;
    }

    /// <summary>
    /// A built-up attack: the ball is carried, a defender is beaten, a pass finds a
    /// runner and the shot comes from somewhere. It is the only branch with more than one
    /// step in it, because it is the only one where more than one thing happened.
    /// </summary>
    private void SimulateAttackSequence(MatchState state, List<MatchEngineEvent> events, bool home)
    {
        var attackers = Outfield(home ? state.HomeLineup : state.AwayLineup);
        var defenders = Outfield(home ? state.AwayLineup : state.HomeLineup);

        if (attackers.Count == 0 || defenders.Count == 0)
        {
            return;
        }

        var teamId = home ? state.HomeTeam.Id : state.AwayTeam.Id;
        var teamName = home ? state.HomeTeam.Name : state.AwayTeam.Name;

        var builder = attackers[_random.Next(0, attackers.Count)];
        var marker = defenders[_random.Next(0, defenders.Count)];

        SetPossession(state, home, builder);
        events.Add(Emit(
            state,
            MatchEventType.BuildUp,
            teamId,
            builder.PlayerId,
            // Both names, because the bank has lines that talk about the man in front of
            // the carrier as well as the carrier.
            MatchNarration.Say(
                _random,
                MatchNarration.BuildUp,
                builder.Name,
                marker.Name),
            "build-up"));

        // Dribbling against strength: the ball carrier is trying something, and the man in
        // front of him is the one who decides whether it works.
        if (_random.NextDouble() >= 0.52 + (builder.Dribbling - marker.Strength) * 0.018)
        {
            SetPossession(state, !home, marker);
            events.Add(Emit(
                state,
                MatchEventType.BuildUp,
                home ? state.AwayTeam.Id : state.HomeTeam.Id,
                marker.PlayerId,
                MatchNarration.Say(
                    _random,
                    MatchNarration.DuelLost,
                    marker.Name,
                    builder.Name),
                "tackle"));

            return;
        }

        var others = attackers.Where(player => player.PlayerId != builder.PlayerId).ToList();
        var shooter = others.Count > 0 ? others[_random.Next(0, others.Count)] : builder;

        SetPossession(state, home, shooter);
        events.Add(Emit(
            state,
            MatchEventType.BuildUp,
            teamId,
            shooter.PlayerId,
            MatchNarration.Say(
                _random,
                MatchNarration.PassFoundRunner,
                builder.Name,
                shooter.Name),
            "build-up"));

        CountShot(state, home);

        var onTargetChance = Math.Clamp(
            0.48 + (shooter.Accuracy - 10) * 0.025 + (shooter.Dribbling - 10) * 0.015,
            MatchRules.MinOnTargetChance,
            0.90);

        if (_random.NextDouble() > onTargetChance)
        {
            events.Add(Emit(
                state,
                MatchEventType.Shot,
                teamId,
                shooter.PlayerId,
                MatchNarration.Say(
                    _random,
                    MatchNarration.ShotPressed,
                    shooter.Name),
                "shot"));

            return;
        }

        CountShotOnTarget(state, home);
        ResolveShot(state, events, home, shooter, reboundAllowed: true);
    }

    /// <summary>
    /// A shot from open play, taken on the strength of the chance the side created: a
    /// team that cannot get to the final third still has shots, they are just worse ones.
    /// </summary>
    private void SimulateOpenPlayShot(
        MatchState state,
        List<MatchEngineEvent> events,
        bool home,
        double attackStrength,
        double opponentsAttack)
    {
        var lineup = home ? state.HomeLineup : state.AwayLineup;
        var teamId = home ? state.HomeTeam.Id : state.AwayTeam.Id;

        var shooters = lineup
            .Where(player => player.IsOnPitch && player.Position is Position.ATT or Position.MID)
            .ToList();

        if (shooters.Count == 0)
        {
            return;
        }

        var shooter = shooters[_random.Next(0, shooters.Count)];
        var total = attackStrength + opponentsAttack;

        var chance = Math.Clamp(
            (total <= 0 ? 0.5 : attackStrength / total) * MatchRules.OnTargetFromStrength
                + (_random.NextDouble() - 0.5) * MatchRules.OnTargetSwing,
            MatchRules.MinOnTargetChance - 0.08,
            MatchRules.MaxOnTargetChance);

        CountShot(state, home);

        if (_random.NextDouble() >= chance)
        {
            events.Add(Emit(
                state,
                MatchEventType.Shot,
                teamId,
                shooter.PlayerId,
                MatchNarration.Say(
                    _random,
                    MatchNarration.ShotOffTarget,
                    shooter.Name),
                "shot"));

            return;
        }

        SetPossession(state, home, shooter);
        CountShotOnTarget(state, home);
        ResolveShot(state, events, home, shooter, reboundAllowed: false);
    }

    /// <summary>
    /// A shot on target, and everything that can happen to it: a goal, a save, or a save
    /// followed by the ball coming loose in front of somebody who was not expecting it.
    ///
    /// There is no third option. A club playing against an empty net does not score into
    /// it — that is what a team without a goalkeeper is punished for, and if the ball finds
    /// the net it is because somebody is standing there.
    /// </summary>
    private void ResolveShot(
        MatchState state,
        List<MatchEngineEvent> events,
        bool home,
        MatchPlayerSnapshot shooter,
        bool reboundAllowed)
    {
        var defendingLineup = home ? state.AwayLineup : state.HomeLineup;
        var teamId = home ? state.HomeTeam.Id : state.AwayTeam.Id;
        var keeper = GoalkeeperOf(defendingLineup);

        if (keeper is null)
        {
            events.Add(Emit(
                state,
                MatchEventType.Shot,
                teamId,
                shooter.PlayerId,
                $"{shooter.Name} finaliza, mas não há goleiro para defender",
                "shot"));

            return;
        }

        var shotPower = shooter.Accuracy * 0.65 + shooter.Dribbling * 0.2 + shooter.Speed * 0.15;
        var savePower = PlayerMetric.KeeperAbility(keeper) * (0.7 + keeper.Energy / 250.0);

        var goalChance = Math.Clamp(
            (shotPower - savePower + 14) / 54.0,
            MatchRules.MinGoalChance,
            MatchRules.MaxGoalChance);

        if (_random.NextDouble() < goalChance)
        {
            ScoreGoal(state, events, home, shooter);
            return;
        }

        CountSave(state, home, keeper);

        events.Add(Emit(
            state,
            MatchEventType.Save,
            home ? state.AwayTeam.Id : state.HomeTeam.Id,
            keeper.PlayerId,
            MatchNarration.Say(
                _random,
                MatchNarration.Save,
                keeper.Name),
            "save"));

        if (!reboundAllowed || _random.NextDouble() >= MatchRules.ReboundChance)
        {
            return;
        }

        var rebounders = Outfield(home ? state.HomeLineup : state.AwayLineup)
            .Where(player => player.PlayerId != shooter.PlayerId)
            .ToList();

        if (rebounders.Count == 0)
        {
            return;
        }

        var second = rebounders[_random.Next(0, rebounders.Count)];
        SetPossession(state, home, second);
        CountShot(state, home);
        CountShotOnTarget(state, home);

        if (_random.NextDouble() < MatchRules.ReboundGoalChance)
        {
            ScoreGoal(state, events, home, second, rebound: true);
            return;
        }

        CountSave(state, home, keeper);

        events.Add(Emit(
            state,
            MatchEventType.Save,
            home ? state.AwayTeam.Id : state.HomeTeam.Id,
            keeper.PlayerId,
            MatchNarration.Say(
                _random,
                MatchNarration.SaveAndHoldRebound,
                keeper.Name,
                second.Name),
            "save"));
    }

    private void ScoreGoal(
        MatchState state,
        List<MatchEngineEvent> events,
        bool home,
        MatchPlayerSnapshot scorer,
        bool rebound = false)
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

        events.Add(Emit(
            state,
            MatchEventType.GoalScored,
            home ? state.HomeTeam.Id : state.AwayTeam.Id,
            scorer.PlayerId,
            MatchNarration.Say(
                _random,
                rebound ? MatchNarration.GoalRebound : MatchNarration.Goal,
                home ? state.HomeTeam.Name : state.AwayTeam.Name,
                scorer.Name),
            "goal"));

        Hold(state);
    }

    /// <summary>
    /// A corner: a defensive clearance that turned into a set piece. The odd one is headed
    /// in, and it does not have to be anybody's fault.
    /// </summary>
    /// <summary>
    /// A save, counted for the side that made it and for the man who made it.
    ///
    /// `home` is the side that was attacking, so the save belongs to the other one: the
    /// goalkeeper who stops a shot is the one doing the defending, whoever sent the ball.
    /// </summary>
    private static void CountSave(MatchState state, bool home, MatchPlayerSnapshot keeper)
    {
        if (home)
        {
            state.AwaySaves++;
        }
        else
        {
            state.HomeSaves++;
        }

        keeper.MatchSaves++;
    }

    private void SimulateCorner(MatchState state, List<MatchEngineEvent> events, bool home)
    {
        var teamId = home ? state.HomeTeam.Id : state.AwayTeam.Id;
        var teamName = home ? state.HomeTeam.Name : state.AwayTeam.Name;

        CountCorner(state, home);

        var takers = Outfield(home ? state.HomeLineup : state.AwayLineup);
        var taker = takers.Count > 0 ? takers[_random.Next(0, takers.Count)] : null;
        SetPossession(state, home, taker);

        events.Add(Emit(
            state,
            MatchEventType.Corner,
            teamId,
            taker?.PlayerId,
            MatchNarration.Say(
                _random,
                MatchNarration.Corner,
                teamName),
            "corner"));
    }

    /// <summary>
    /// An own goal, which is an error and not a piece of luck.
    ///
    /// It has exactly one cause in football: a defender doing his job — trying to play the
    /// ball past his own goalkeeper, or clear it under pressure — and putting it into his
    /// own net. That is why it comes from a defensive action and not from a corner, which is
    /// only a ball arriving in a dangerous place and needs nobody to be wrong.
    ///
    /// The chance is tiny on purpose, because the point of an own goal is that it is worth
    /// arguing about for the rest of the season.
    /// </summary>
    private void SimulateOwnGoal(MatchState state, List<MatchEngineEvent> events, bool home)
    {
        var attackingTeamId = home ? state.HomeTeam.Id : state.AwayTeam.Id;
        var attackingTeamName = home ? state.HomeTeam.Name : state.AwayTeam.Name;

        var defenders = Outfield(home ? state.AwayLineup : state.HomeLineup);
        if (defenders.Count == 0)
        {
            return;
        }

        var own = defenders[_random.Next(0, defenders.Count)];

        if (home)
        {
            state.AwayScore++;
            state.LastScoreAway = state.Minute;
        }
        else
        {
            state.HomeScore++;
            state.LastScoreHome = state.Minute;
        }

        own.MatchOwnGoals++;

        // The side that was on the ball loses it in the one place it should never lose it,
        // and the other side has nothing to do with the goal.
        SetPossession(state, !home, null);

        events.Add(Emit(
            state,
            MatchEventType.OwnGoalScored,
            attackingTeamId,
            own.PlayerId,
            MatchNarration.Say(
                _random,
                MatchNarration.GoalOwn,
                own.Name,
                attackingTeamName),
            "own-goal"));

        Hold(state);
    }

    /// <summary>
    /// A foul by the side that was defending. The only way a penalty is born, so the number
    /// that decides how often the eleven metres show up lives with it.
    /// </summary>
    private void SimulateFoul(MatchState state, List<MatchEngineEvent> events, bool home)
    {
        var foulingTeamIsHome = !home;
        var foulingTeamId = foulingTeamIsHome ? state.HomeTeam.Id : state.AwayTeam.Id;
        var foulingTeamName = foulingTeamIsHome ? state.HomeTeam.Name : state.AwayTeam.Name;

        CountFoul(state, foulingTeamIsHome);

        var victims = Outfield(home ? state.HomeLineup : state.AwayLineup);
        var offenders = Outfield(home ? state.AwayLineup : state.HomeLineup);

        var victim = victims.Count > 0 ? victims[_random.Next(0, victims.Count)] : null;
        var offender = offenders.Count > 0 ? offenders[_random.Next(0, offenders.Count)] : null;

        SetPossession(state, home, victim);

        events.Add(Emit(
            state,
            MatchEventType.Foul,
            foulingTeamId,
            offender?.PlayerId,
            offender is null || victim is null
                ? $"{foulingTeamName} comete falta"
                : MatchNarration.Say(
                    _random,
                    MatchNarration.Foul,
                    offender.Name,
                    victim.Name),
            "foul"));

        if (victim is not null && _random.NextDouble() < MatchRules.PenaltyFromFoulChance)
        {
            AwardPenalty(state, events, offendingTeamIsHome: foulingTeamIsHome);
        }
    }

    /// <summary>
    /// A midfield duel: a minute with no shot in it is a minute of passing, pressing and
    /// second balls, and the ball ends up with whoever won the last one of those.
    /// </summary>
    private void SimulateDuel(MatchState state, List<MatchEngineEvent> events, bool home)
    {
        var teamId = home ? state.HomeTeam.Id : state.AwayTeam.Id;
        var teamName = home ? state.HomeTeam.Name : state.AwayTeam.Name;

        var attackers = Outfield(home ? state.HomeLineup : state.AwayLineup);
        var defenders = Outfield(home ? state.AwayLineup : state.HomeLineup);

        if (attackers.Count == 0 || defenders.Count == 0)
        {
            return;
        }

        var carrier = attackers[_random.Next(0, attackers.Count)];
        var defender = defenders[_random.Next(0, defenders.Count)];

        if (_random.NextDouble() < MatchRules.DuelSuccessChance)
        {
            SetPossession(state, home, carrier);
            events.Add(Emit(
                state,
                MatchEventType.BuildUp,
                teamId,
                carrier.PlayerId,
                MatchNarration.Say(
                    _random,
                    MatchNarration.DuelWon,
                    carrier.Name,
                    defender.Name),
                "build-up"));

            return;
        }

        SetPossession(state, !home, defender);
        events.Add(Emit(
            state,
            MatchEventType.BuildUp,
            home ? state.AwayTeam.Id : state.HomeTeam.Id,
            defender.PlayerId,
            MatchNarration.Say(
                _random,
                MatchNarration.DuelLost,
                defender.Name,
                carrier.Name),
            "tackle"));
    }

    // --- Discipline -------------------------------------------------------------

    private void ShowYellowCard(MatchState state, List<MatchEngineEvent> events, bool home)
    {
        var candidates = Outfield(home ? state.HomeLineup : state.AwayLineup);
        if (candidates.Count == 0)
        {
            return;
        }

        var player = candidates[_random.Next(0, candidates.Count)];
        var teamId = home ? state.HomeTeam.Id : state.AwayTeam.Id;
        var teamName = home ? state.HomeTeam.Name : state.AwayTeam.Name;

        SetPossession(state, home, player);
        CountCard(state, home);

        // A second yellow of the match is a red, and there is nothing to be done about it
        // but leave the pitch.
        if (player.MatchYellowCards > 0)
        {
            player.SendOff();
            player.MatchYellowCards++;

            events.Add(Emit(
                state,
                MatchEventType.RedCardShown,
                teamId,
                player.PlayerId,
                MatchNarration.Say(
                    _random,
                    MatchNarration.SecondYellow,
                    player.Name,
                    teamName),
                "red-card"));

            Hold(state);
            EnsureGoalkeeper(state, events, home);

            return;
        }

        player.MatchYellowCards++;

        events.Add(Emit(
            state,
            MatchEventType.YellowCardShown,
            teamId,
            player.PlayerId,
            MatchNarration.Say(
                _random,
                MatchNarration.YellowCard,
                player.Name),
            "yellow-card"));
    }

    private void ShowDirectRedCard(MatchState state, List<MatchEngineEvent> events, bool home)
    {
        var candidates = Outfield(home ? state.HomeLineup : state.AwayLineup);
        if (candidates.Count == 0)
        {
            return;
        }

        var player = candidates[_random.Next(0, candidates.Count)];
        var teamId = home ? state.HomeTeam.Id : state.AwayTeam.Id;
        var teamName = home ? state.HomeTeam.Name : state.AwayTeam.Name;

        SetPossession(state, home, player);
        CountCard(state, home);
        player.SendOff();

        events.Add(Emit(
            state,
            MatchEventType.RedCardShown,
            teamId,
            player.PlayerId,
            MatchNarration.Say(
                _random,
                MatchNarration.RedCard,
                player.Name,
                teamName),
            "red-card"));

        Hold(state);
        EnsureGoalkeeper(state, events, home);
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
            MatchNarration.Say(
                _random,
                MatchNarration.PenaltyAwarded,
                fouledTeamName),
            "penalty"));

        if (state.ManagerSelectsPenaltyTaker(fouledTeamId))
        {
            state.PenaltyAwaitingSelection = true;
            state.PenaltyTeam = offendingTeamIsHome ? 2 : 1;
            return;
        }

        var lineup = offendingTeamIsHome ? state.AwayLineup : state.HomeLineup;
        var taker = BestPenaltyTaker(
            lineup,
            GoalkeeperOf(offendingTeamIsHome ? state.HomeLineup : state.AwayLineup));

        if (taker is null)
        {
            return;
        }

        events.AddRange(TakePenalty(state, home: !offendingTeamIsHome, taker));
    }

    // --- Injuries ---------------------------------------------------------------

    /// <summary>
    /// A knock. What it costs, whether it ends the match, and how long the absence will be
    /// are three different questions, and the engine answers all three: a light knock is a
    /// player who stays down and is markedly more likely to be hurt again, a serious one is
    /// a player who leaves and a month of football.
    /// </summary>
    private void TriggerInjury(
        MatchState state,
        List<MatchEngineEvent> events,
        bool home,
        MatchPlayerSnapshot player)
    {
        var teamId = home ? state.HomeTeam.Id : state.AwayTeam.Id;
        var teamName = home ? state.HomeTeam.Name : state.AwayTeam.Name;

        var severe = PlayerMetric.IsSevereInjury(player, _random.NextDouble());
        var loss = severe
            ? _random.Next(MatchRules.MinEnergyLostSevere, MatchRules.MaxEnergyLostSevere)
            : _random.Next(MatchRules.MinEnergyLostLight, MatchRules.MaxEnergyLostLight);

        player.DrainEnergy(loss, MatchRules.EnergyFloorAfterInjury);
        state.InjuriesThisMatch++;

        if (!severe)
        {
            player.Injure(Injury.Light);

            events.Add(Emit(
                state,
                MatchEventType.PlayerInjured,
                teamId,
                player.PlayerId,
                $"{teamName}: " + MatchNarration.Say(
                    _random,
                    MatchNarration.InjuryLight,
                    player.Name),
                "injury"));

            return;
        }

        player.Injure(Injury.Grave, _random.Next(MatchRules.MinMatchesOutSevere, MatchRules.MaxMatchesOutSevere));

        events.Add(Emit(
            state,
            MatchEventType.PlayerInjured,
            teamId,
            player.PlayerId,
            $"{teamName}: " + MatchNarration.Say(
                _random,
                MatchNarration.InjuryGrave,
                player.Name,
                teamName),
            "injury"));

        Hold(state);
        ForceInjurySubstitution(state, events, home, player);
    }

    /// <summary>
    /// Puts somebody on the pitch in place of a player who cannot continue.
    ///
    /// This is the club's own bench, not a choice about who else might be tired, so the
    /// best available man comes on. When it is the goalkeeper who went, a real goalkeeper
    /// beats a better outfielder every time, and that preference is in
    /// <see cref="EnsureGoalkeeper"/> so the two paths can never disagree about it.
    /// </summary>
    private void ForceInjurySubstitution(
        MatchState state,
        List<MatchEngineEvent> events,
        bool home,
        MatchPlayerSnapshot outgoing)
    {
        var lineup = home ? state.HomeLineup : state.AwayLineup;
        var bench = home ? state.HomeBench : state.AwayBench;
        var teamId = home ? state.HomeTeam.Id : state.AwayTeam.Id;
        var teamName = home ? state.HomeTeam.Name : state.AwayTeam.Name;

        var used = home ? state.SubstitutionsHome : state.SubstitutionsAway;

        if (used < MatchRules.MaxSubstitutions)
        {
            var available = AvailableOnBench(bench);
            var incoming = BestReplacement(available, outgoing.KeepsGoal, outgoing.Position);

            if (incoming is not null)
            {
                PerformSubstitution(state, events, home, outgoing, incoming, MatchNarration.SubstitutionForInjury, teamId, teamName);
                return;
            }
        }

        // The bench is empty or the club is out of changes. There is nobody to send on, so
        // the club plays on a man down — unless it is the goalkeeper who went, in which
        // case somebody on the pitch has to take the gloves.
        EnsureGoalkeeper(state, events, home);
    }

    // --- Substitutions ----------------------------------------------------------

    /// <summary>
    /// Plays the substitutions of the club nobody is watching. The manager's club is his
    /// to change and the engine does not touch it; every other club is played with the
    /// three triggers a real manager is left with once the goalkeeper question is settled —
    /// a tired team at the interval, a booked player who cannot afford another foul, and a
    /// man who has nothing left late on.
    /// </summary>
    private void SubstituteForTheUnwatchedClub(MatchState state, List<MatchEngineEvent> events)
    {
        SubstituteForTheUnwatchedClub(state, events, home: true);
        SubstituteForTheUnwatchedClub(state, events, home: false);
    }

    private void SubstituteForTheUnwatchedClub(MatchState state, List<MatchEngineEvent> events, bool home)
    {
        var teamId = home ? state.HomeTeam.Id : state.AwayTeam.Id;

        // The manager decides his own club. When nobody is watching, both sides are played
        // by the engine — which is what a fixture simulated from the calendar is.
        if (state.ManagerTeamId is { } managed && managed == teamId)
        {
            return;
        }

        var lineup = home ? state.HomeLineup : state.AwayLineup;
        var bench = home ? state.HomeBench : state.AwayBench;
        var teamName = home ? state.HomeTeam.Name : state.AwayTeam.Name;

        var used = home ? state.SubstitutionsHome : state.SubstitutionsAway;
        if (used >= MatchRules.MaxSubstitutions)
        {
            return;
        }

        var starters = lineup.Where(player => player.IsOnPitch).ToList();
        var available = AvailableOnBench(bench);

        if (starters.Count == 0 || available.Count == 0)
        {
            return;
        }

        // An empty goal is not one of the four triggers, because it is not a decision:
        // <see cref="EnsureGoalkeeper"/> has already dealt with it before the club ever
        // got here, and a manager who wanted an outfielder in goal is welcome to ask.

        var outfielders = Outfield(starters);
        if (outfielders.Count == 0)
        {
            return;
        }

        // After the interval there is a short window to correct a shape that is not
        // working, or a man who is already spent. A change at minute 20 is a different
        // decision from the same change at minute 80.
        if (state.Half == 1
            && state.Minute >= MatchRules.SecondHalfSubWindowStart
            && state.Minute < MatchRules.SecondHalfSubWindowEnd
            && _random.NextDouble() < MatchRules.HalftimeSubstitutionChance)
        {
            var tired = outfielders
                .Where(player => player.Energy < MatchRules.EnergyForFatigueSubstitution)
                .OrderBy(player => player.Energy)
                .FirstOrDefault();

            if (tired is not null)
            {
                var replacement = BestReplacement(available, keepsGoal: false, tired.Position);

                if (replacement is not null)
                {
                    PerformSubstitution(state, events, home, tired, replacement, MatchNarration.SubstitutionForFatigue, teamId, teamName);
                    return;
                }
            }

            var weakest = outfielders
                .OrderBy(player => PlayerMetric.Metric(player))
                .FirstOrDefault();

            if (weakest is not null && _random.NextDouble() < 0.55)
            {
                var replacement = BestReplacement(available, keepsGoal: false, weakest.Position);

                if (replacement is not null)
                {
                    PerformSubstitution(state, events, home, weakest, replacement, MatchNarration.SubstitutionTactical, teamId, teamName);
                    return;
                }
            }
        }

        // A booked player on low energy is one more foul from being sent off, and a second
        // yellow is a worse use of the bench than a straight swap.
        if (state.Minute >= MatchRules.RiskySubstitutionMinute
            && _random.NextDouble() < MatchRules.RiskySubstitutionChance)
        {
            var risky = outfielders
                .Where(player => player.MatchYellowCards >= 1 && player.Energy < MatchRules.EnergyForRiskySubstitution)
                .ToList();

            if (risky.Count > 0)
            {
                var atRisk = risky[_random.Next(0, risky.Count)];
                var replacement = BestReplacement(available, keepsGoal: false, atRisk.Position);

                if (replacement is not null)
                {
                    PerformSubstitution(
                        state, events, home,
                        atRisk,
                        replacement,
                        MatchNarration.SubstitutionRisk,
                        teamId, teamName);

                    return;
                }
            }
        }

        // Late on, a last change for a man who has nothing left.
        if (state.Minute >= MatchRules.FatigueSubstitutionMinute
            && _random.NextDouble() < MatchRules.FatigueSubstitutionChance)
        {
            var spent = outfielders
                .Where(player => player.Energy < MatchRules.EnergyForFatigueSubstitution)
                .OrderBy(player => player.Energy)
                .FirstOrDefault();

            var replacement = spent is null
                ? null
                : BestReplacement(available, keepsGoal: false, spent.Position);

            if (spent is not null && replacement is not null)
            {
                PerformSubstitution(state, events, home, spent, replacement, MatchNarration.SubstitutionForFatigue, teamId, teamName);
            }
        }
    }

    /// <summary>
    /// The swap itself: counted, announced, and the shape of the side recalculated, because
    /// replacing a striker for a defender does not only change two names.
    /// </summary>
    private void PerformSubstitution(
        MatchState state,
        List<MatchEngineEvent> events,
        bool home,
        MatchPlayerSnapshot outgoing,
        MatchPlayerSnapshot incoming,
        string[] reasonPhrase,
        Guid teamId,
        string teamName)
    {
        var lineup = home ? state.HomeLineup : state.AwayLineup;

        if (!MatchSubstitution.CanSwap(lineup, outgoing, incoming))
        {
            return;
        }

        MatchSubstitution.Swap(state, home, outgoing, incoming);

        // Before the interval these are remembered, because the second half opens by saying
        // whether either manager changed anything — and that sentence is about names, not
        // about a count.
        if (state.Half == 0)
        {
            state.FirstHalfChanges.Add($"{teamName}: {incoming.Name} no lugar de {outgoing.Name}");
        }

        events.Add(Emit(
            state,
            MatchEventType.SubstitutionMade,
            teamId,
            incoming.PlayerId,
            $"{teamName}: " + MatchNarration.Say(
                _random,
                reasonPhrase,
                incoming.Name,
                outgoing.Name),
            "substitution"));

        Hold(state);
    }

    /// <summary>
    /// The best player on the bench for the job, which is not always the best player on the
    /// bench. Two things outrank a rating:
    ///
    /// - a real goalkeeper beats a better outfielder, always, when the man coming off is
    ///   the one in goal;
    /// - and then the line matters. A manager who has replaced four defenders with
    ///   midfielders has not made four good changes, he has turned his team into something
    ///   else — and since the engine reads the shape off the eleven, the shape is what the
    ///   rest of the match is measured against.
    ///
    /// Only when the bench has nobody for the line does the best man on it play, whatever
    /// he plays.
    /// </summary>
    private static MatchPlayerSnapshot? BestReplacement(
        List<MatchPlayerSnapshot> available,
        bool keepsGoal,
        Position replaces)
    {
        if (available.Count == 0)
        {
            return null;
        }

        if (keepsGoal)
        {
            var keeper = available
                .Where(player => player.Position == Position.GK)
                .OrderByDescending(player => PlayerMetric.KeeperAbility(player))
                .FirstOrDefault();

            if (keeper is not null)
            {
                return keeper;
            }
        }

        // A man who has already left the pitch is not a candidate: a substitute is spent,
        // and the engine holds itself to the same rule the manager is held to.
        var spendable = available.Where(player => !player.SubbedOff).ToList();
        if (spendable.Count == 0)
        {
            return null;
        }

        var outfield = spendable
            .Where(player => player.Position != Position.GK)
            .ToList();

        var pool = outfield.Count > 0 ? outfield : spendable;

        return pool
                .Where(player => player.Position == replaces)
                .OrderByDescending(player => PlayerMetric.Metric(player))
                .FirstOrDefault()
            ?? pool
                .OrderByDescending(player => PlayerMetric.Metric(player))
                .FirstOrDefault();
    }

    /// <summary>
    /// The player a club can least afford to lose from the pitch, used when the club has to
    /// take somebody off and has no bench to put anybody on with. Giving the gloves away is
    /// the last thing that should happen, so the goalkeeper goes last.
    /// </summary>
    private static MatchPlayerSnapshot LeastValuable(List<MatchPlayerSnapshot> starters) =>
        starters
            .OrderBy(player => player.KeepsGoal ? double.MaxValue : PlayerMetric.Metric(player))
            .First();

    /// <summary>
    /// Makes sure the club still has someone keeping the goal after losing one, and that
    /// the somebody is a real goalkeeper whenever the club still has one on the bench.
    ///
    /// The order is the whole rule: a club whose keeper has just been sent off or injured
    /// reaches for the reserve keeper before it thinks about anything else, because an
    /// outfielder in goal is a hole in the side and a hole is a hole. Only when the bench
    /// has nobody left is a defender promoted, and a club that runs out of goalkeepers does
    /// not stop playing football — it just plays against an empty net.
    ///
    /// This runs for both clubs on every tick. The manager's own club is not substituted
    /// here, because he decides that himself; what he is told is that his club is about to
    /// play an outfielder in goal, which is a thing no manager wants.
    /// </summary>
    private void EnsureGoalkeeper(MatchState state, List<MatchEngineEvent> events, bool home)
    {
        var lineup = home ? state.HomeLineup : state.AwayLineup;
        var bench = home ? state.HomeBench : state.AwayBench;
        var teamId = home ? state.HomeTeam.Id : state.AwayTeam.Id;
        var teamName = home ? state.HomeTeam.Name : state.AwayTeam.Name;

        if (MatchSubstitution.HasGoalkeeper(lineup))
        {
            return;
        }

        var used = home ? state.SubstitutionsHome : state.SubstitutionsAway;
        var remaining = lineup.Where(player => player.IsOnPitch).ToList();

        if (used < MatchRules.MaxSubstitutions && remaining.Count > 0)
        {
            var reserve = AvailableOnBench(bench)
                .Where(player => player.Position == Position.GK)
                .OrderByDescending(player => PlayerMetric.KeeperAbility(player))
                .FirstOrDefault();

            if (reserve is not null)
            {
                PerformSubstitution(
                    state, events, home,
                    LeastValuable(remaining),
                    reserve,
                    MatchNarration.KeeperPromoted,
                    teamId, teamName);

                return;
            }
        }

        // A defender is who takes the gloves: he is the one whose job is to be in the way,
        // and a club that has to do this has already lost the argument.
        var promoted = Outfield(lineup)
            .OrderByDescending(player => player.Speed + player.Strength * 1.2 + player.Heading * 1.1 + player.Accuracy)
            .FirstOrDefault();

        if (promoted is null)
        {
            return;
        }

        promoted.PromoteToGoalkeeper();

        if (home)
        {
            state.HomeFormation = Formation.FromComposition(lineup);
        }
        else
        {
            state.AwayFormation = Formation.FromComposition(lineup);
        }

        events.Add(Emit(
            state,
            MatchEventType.KeeperPromoted,
            teamId,
            promoted.PlayerId,
            $"{teamName} sem goleiro: {promoted.Name} assume a meta",
            "emergency-keeper"));
    }

    // --- Picking and counting ---------------------------------------------------

    /// <summary>
    /// The men on a bench who can actually be sent on: not sent off, and not already
    /// spent by a substitution he has been through.
    /// </summary>
    private static List<MatchPlayerSnapshot> AvailableOnBench(List<MatchPlayerSnapshot> bench) =>
        bench.Where(player => !player.RedCard && !player.SubbedOff).ToList();

    /// <summary>
    /// The outfield players of a lineup, on the pitch. A minute of football is played by
    /// men in boots, and a keeper is one of them only in the sense that he is standing
    /// there.
    /// </summary>
    /// <summary>
    /// The eleven as one line of text, grouped by the line each man plays in.
    ///
    /// Grouped rather than numbered because a team sheet is a shape, not a shopping list:
    /// reading "Goleiro: X; Defesa: A, B, C, D" says the same thing as eleven numbers and
    /// takes a third of the space in a feed row.
    /// </summary>
    private static string DescribeEleven(IEnumerable<MatchPlayerSnapshot> lineup)
    {
        var byLine = lineup
            .Where(player => player.IsOnPitch)
            .GroupBy(player => player.Position)
            .ToDictionary(group => group.Key, group => string.Join(", ", group.Select(player => player.Name)));

        var parts = new List<string>();

        if (byLine.TryGetValue(Position.GK, out var keeper))
        {
            parts.Add($"Goleiro: {keeper}");
        }

        if (byLine.TryGetValue(Position.DEF, out var defence))
        {
            parts.Add($"Defesa: {defence}");
        }

        if (byLine.TryGetValue(Position.MID, out var midfield))
        {
            parts.Add($"Meio: {midfield}");
        }

        if (byLine.TryGetValue(Position.ATT, out var attack))
        {
            parts.Add($"Ataque: {attack}");
        }

        return string.Join("; ", parts);
    }

    private static List<MatchPlayerSnapshot> Outfield(IEnumerable<MatchPlayerSnapshot> lineup) =>
        lineup
            .Where(player => player.IsOnPitch && player.Position != Position.GK && !player.EmergencyGK)
            .ToList();

    /// <summary>
    /// The players who can be hurt. A goalkeeper is not in the running for a knock unless
    /// his club has nobody else in goal to lose.
    /// </summary>
    private static List<MatchPlayerSnapshot> Vulnerable(List<MatchPlayerSnapshot> lineup)
    {
        var onPitch = lineup.Where(player => player.IsOnPitch).ToList();
        var outfield = onPitch.Where(player => player.Position != Position.GK && !player.EmergencyGK).ToList();

        return outfield.Count > 0 ? outfield : onPitch;
    }

    private static MatchPlayerSnapshot? FirstOf(List<MatchPlayerSnapshot> lineup) =>
        lineup.FirstOrDefault(player => player.IsOnPitch);

    private static void CountShot(MatchState state, bool home)
    {
        if (home)
        {
            state.HomeShots++;
        }
        else
        {
            state.AwayShots++;
        }
    }

    private static void CountShotOnTarget(MatchState state, bool home)
    {
        if (home)
        {
            state.HomeShotsOnTarget++;
        }
        else
        {
            state.AwayShotsOnTarget++;
        }
    }

    private static void CountCorner(MatchState state, bool home)
    {
        if (home)
        {
            state.HomeCorners++;
        }
        else
        {
            state.AwayCorners++;
        }
    }

    private static void CountCard(MatchState state, bool home)
    {
        if (home)
        {
            state.HomeCards++;
        }
        else
        {
            state.AwayCards++;
        }
    }

    private static void CountFoul(MatchState state, bool home)
    {
        if (home)
        {
            state.HomeFouls++;
        }
        else
        {
            state.AwayFouls++;
        }
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
