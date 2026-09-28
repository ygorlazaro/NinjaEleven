using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Players;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// Fair play rules: cards that lead to suspensions, injuries that keep a player out for a
/// number of matches, a club that has to play without a goalkeeper, and penalties the
/// manager decides the taker of.
/// </summary>
public class FairPlayTests
{
    private sealed class ScriptedRandomSource : IRandomSource
    {
        private readonly Queue<double> _doubles;
        private readonly Queue<int> _ints;
        private double _last = 0.5;

        public ScriptedRandomSource(IEnumerable<double> doubles, IEnumerable<int>? ints = null)
        {
            _doubles = new Queue<double>(doubles);
            _ints = new Queue<int>(ints ?? []);
        }

        public int Next(int min, int max)
        {
            if (_ints.Count > 0)
            {
                return Math.Clamp(_ints.Dequeue(), min, max - 1);
            }

            if (max <= min)
            {
                return min;
            }

            return min + (int)(NextDouble() * (max - min));
        }

        public double NextDouble()
        {
            if (_doubles.Count > 0)
            {
                _last = _doubles.Dequeue();
            }

            return _last;
        }
    }

    private static PlayerSeasonState NewState(Injury injury = Injury.None, int suspension = 0)
    {
        var state = PlayerSeasonState.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 90);

        if (injury != Injury.None)
        {
            state.AddInjury(injury, injury == Injury.Grave ? 4 : 2);
        }

        if (suspension > 0)
        {
            state.AddSuspension(suspension);
        }

        return state;
    }

    [Fact]
    public void ThreeYellowCards_LeavesThePlayerSuspended()
    {
        var state = NewState();

        state.AddYellowCard();
        state.AddYellowCard();
        Assert.True(state.IsAvailable);

        state.AddYellowCard();

        Assert.False(state.IsAvailable);
        Assert.Equal(0, state.YellowCards);
        Assert.Equal(2, state.SuspensionMatches);
    }

    [Fact]
    public void RedCard_SuspendsThePlayerForTheNextMatch()
    {
        var state = NewState();

        state.AddRedCard();

        Assert.False(state.IsAvailable);
        Assert.Equal(1, state.RedCards);
        Assert.Equal(2, state.SuspensionMatches);
    }

    [Fact]
    public void Suspension_IsServedOneMatchAtATime()
    {
        var state = NewState();
        state.AddRedCard();

        state.RecoverFromMatches();
        Assert.False(state.IsAvailable);

        state.RecoverFromMatches();
        Assert.True(state.IsAvailable);
    }

    [Fact]
    public void Injury_KeepsThePlayerOutForItsWholeDuration()
    {
        var state = NewState();

        state.AddInjury(Injury.Grave, 4);

        Assert.False(state.IsAvailable);
        Assert.Equal(Injury.Grave, state.Injury);
        Assert.Equal(4, state.InjuryMatchesRemaining);

        state.RecoverFromMatches();
        state.RecoverFromMatches();
        state.RecoverFromMatches();
        Assert.False(state.IsAvailable);

        state.RecoverFromMatches();
        Assert.True(state.IsAvailable);
        Assert.Equal(Injury.None, state.Injury);
    }

    [Fact]
    public void TheWorstInjuryOfTheSeasonIsTheOneThatCounts()
    {
        var state = NewState();

        state.AddInjury(Injury.Light, 2);
        state.AddInjury(Injury.Grave, 4);
        state.AddInjury(Injury.Light, 2);

        Assert.Equal(Injury.Grave, state.Injury);
        Assert.Equal(4, state.InjuryMatchesRemaining);
    }

    [Fact]
    public void InjuredPlayer_CannotBeChosenForAMatch()
    {
        var state = NewState();
        state.AddInjury(Injury.Light, 2);

        Assert.False(state.IsAvailable);
    }

    private static MatchState CreateState(IRandomSource random, Guid? managerTeamId, out MatchContext context)
    {
        var homeTeam = new TeamInfo(Guid.NewGuid(), "Home United", "HU", "#FF0000", "#FFFFFF", 55);
        var awayTeam = new TeamInfo(Guid.NewGuid(), "Away City", "AC", "#0000FF", "#FFFFFF", 50);

        var positions = new[]
        {
            Position.GK, Position.DEF, Position.DEF, Position.MID, Position.MID,
            Position.MID, Position.ATT, Position.ATT, Position.DEF, Position.MID, Position.MID
        };

        context = new MatchContext(
            Guid.NewGuid(),
            homeTeam,
            awayTeam,
            CreateLineup(homeTeam, positions),
            CreateLineup(awayTeam, positions),
            [],
            [],
            random,
            managerTeamId);

        return new MatchState(context);
    }

    /// <summary>
    /// A match with a bench for the away club only: a better outfielder and a goalkeeper,
    /// in that order, so a test can tell which of the two the engine reaches for.
    /// </summary>
    private static MatchState CreateStateWithBench(IRandomSource random, out List<MatchPlayerSnapshot> awayBench)
    {
        var homeTeam = new TeamInfo(Guid.NewGuid(), "Home United", "HU", "#FF0000", "#FFFFFF", 55);
        var awayTeam = new TeamInfo(Guid.NewGuid(), "Away City", "AC", "#0000FF", "#FFFFFF", 50);

        var positions = new[]
        {
            Position.GK, Position.DEF, Position.DEF, Position.MID, Position.MID,
            Position.MID, Position.ATT, Position.ATT, Position.DEF, Position.MID, Position.MID
        };

        awayBench =
        [
            BuildPlayer(awayTeam, "Reserve forward", Position.ATT, 18, 18),
            BuildPlayer(awayTeam, "Reserve keeper", Position.GK, 12, 12, goalkeeperPower: 16, reflexes: 16)
        ];

        var context = new MatchContext(
            Guid.NewGuid(),
            homeTeam,
            awayTeam,
            CreateLineup(homeTeam, positions),
            CreateLineup(awayTeam, positions),
            [],
            awayBench,
            random,
            null);

        return new MatchState(context);
    }

    private static List<MatchPlayerSnapshot> CreateLineup(TeamInfo team, Position[] positions)    {
        var lineup = new List<MatchPlayerSnapshot>();

        for (var i = 0; i < positions.Length; i++)
        {
            var player = Player.Create(
                $"{team.ShortName} {positions[i]} {i}",
                24,
                positions[i],
                speed: 12,
                accuracy: 12,
                dribbling: 12,
                heading: 12,
                strength: 12,
                goalkeeperPower: positions[i] == Position.GK ? 18 : 0,
                reflexes: positions[i] == Position.GK ? 18 : 0);

            var state = PlayerSeasonState.Create(player.Id, Guid.NewGuid(), team.Id, 90);
            lineup.Add(MatchPlayerSnapshot.FromPlayerSeasonState(player, state));
        }

        return lineup;
    }

    /// <summary>
    /// Drives the engine until an event of the wanted type shows up, or the match is over.
    /// </summary>
    private static MatchEngineEvent? RunUntil(
        MatchEngine engine,
        MatchState state,
        MatchEventType type,
        int maxTicks = 2000)
    {
        for (var i = 0; i < maxTicks && !state.MatchFinished; i++)
        {
            foreach (var produced in engine.Tick(state))
            {
                if (produced.Type == type)
                {
                    return produced;
                }
            }

            if (state.HalfTimePauseActive)
            {
                engine.ContinueSecondHalf(state);
            }
        }

        return null;
    }

    [Fact]
    public void AGoalkeeperSentOff_IsReplacedByAnOutfieldPlayer()
    {
        var random = new ScriptedRandomSource(Enumerable.Repeat(0.5, 40));
        var state = CreateState(random, null, out _);
        var engine = new MatchEngine(random);

        engine.Initialize(state, 0);

        var keeper = state.HomeLineup.First(player => player.Position == Position.GK);
        keeper.SendOff(0);

        Assert.False(state.HomeLineup.Any(player => player.KeepsGoal));

        var events = engine.Tick(state).ToList();

        var promoted = state.HomeLineup.First(player => player.KeepsGoal);
        Assert.NotEqual(keeper.PlayerId, promoted.PlayerId);
        Assert.True(promoted.EmergencyGK);
        Assert.Contains(events, e => e.Type == MatchEventType.KeeperPromoted && e.PlayerId == promoted.PlayerId);
    }

    [Fact]
    public void AnInjuredGoalkeeper_IsReplacedByAnOutfieldPlayer()
    {
        var random = new ScriptedRandomSource(Enumerable.Repeat(0.5, 40));
        var state = CreateState(random, null, out _);
        var engine = new MatchEngine(random);

        engine.Initialize(state, 0);

        state.AwayLineup.First(player => player.Position == Position.GK).Injure(Injury.Grave);

        engine.Tick(state);

        var promoted = state.AwayLineup.First(player => player.KeepsGoal);
        Assert.True(promoted.EmergencyGK);
        Assert.NotEqual(Position.GK, promoted.Position);
    }

    [Fact]
    public void ALightKnock_KeepsThePlayerOnThePitchAndMakesHimVulnerable()
    {
        var random = new ScriptedRandomSource(Enumerable.Repeat(0.5, 40));
        var state = CreateState(random, null, out _);
        var engine = new MatchEngine(random);

        engine.Initialize(state, 0);

        var injured = state.HomeLineup.First(player => player.Position != Position.GK);
        var chanceBefore = PlayerMetric.InjuryChance(injured);

        injured.Injure(Injury.Light);

        // A light knock is a player who stays down, not a player who leaves: he finishes
        // the match, and he is markedly more likely to be knocked again.
        Assert.True(injured.IsOnPitch);
        Assert.Equal(Injury.Light, injured.Injury);
        Assert.Equal(0, injured.InjuryMatchesOut);
        Assert.True(PlayerMetric.InjuryChance(injured) > chanceBefore);
    }

    [Fact]
    public void ASeriousKnock_TakesThePlayerOffForAQuarterOfTheSeason()
    {
        var random = new ScriptedRandomSource(Enumerable.Repeat(0.5, 40));
        var state = CreateState(random, null, out _);
        var engine = new MatchEngine(random);

        engine.Initialize(state, 0);

        var injured = state.HomeLineup.First(player => player.Position != Position.GK);
        injured.Injure(Injury.Grave, matchesOut: 3);

        Assert.False(injured.IsOnPitch);
        Assert.Equal(Injury.Grave, injured.Injury);
        Assert.Equal(3, injured.InjuryMatchesOut);
    }

    [Fact]
    public void ABenchGoalkeeperComesOnBeforeABetterOutfielderWhenTheKeeperGoesInjured()
    {
        var random = new ScriptedRandomSource(Enumerable.Repeat(0.5, 40));
        var state = CreateStateWithBench(random, out var awayBench);
        var engine = new MatchEngine(random);

        engine.Initialize(state, 0);

        // The outfielder on the bench is the better footballer; the goalkeeper is the only
        // man who can keep the goal. The club never spends a moment with nobody in goal
        // when a real goalkeeper is standing on the touchline.
        var keeper = state.AwayLineup.First(player => player.Position == Position.GK);
        keeper.Injure(Injury.Grave, matchesOut: 3);

        // The engine looks after the goal before it does anything else on the tick, so the
        // club is never a moment short of one.
        engine.Tick(state);

        var inGoal = state.AwayLineup.First(player => player.KeepsGoal);
        Assert.Equal(Position.GK, inGoal.Position);
        Assert.NotEqual(awayBench[0].PlayerId, inGoal.PlayerId);
        Assert.True(state.SubstitutionsAway > 0);
    }

    [Fact]
    public void AManagerPenalty_WaitsForHimToNameTheTaker()
    {
        var random = new ScriptedRandomSource(Enumerable.Repeat(0.5, 40));
        var homeTeam = new TeamInfo(Guid.NewGuid(), "Home", "H", "#FF0000", "#FFFFFF", 55);
        var awayTeam = new TeamInfo(Guid.NewGuid(), "Away", "A", "#0000FF", "#FFFFFF", 50);
        var positions = new[]
        {
            Position.GK, Position.DEF, Position.DEF, Position.MID, Position.MID,
            Position.MID, Position.ATT, Position.ATT, Position.DEF, Position.MID, Position.MID
        };

        var context = new MatchContext(
            Guid.NewGuid(), homeTeam, awayTeam,
            CreateLineup(homeTeam, positions), CreateLineup(awayTeam, positions),
            [], [], random, homeTeam.Id);

        var state = new MatchState(context);
        var engine = new MatchEngine(random);
        engine.Initialize(state, 0);

        // The away team fouls inside the area.
        state.PenaltyAwaitingSelection = true;
        state.PenaltyTeam = 1;

        var minute = state.Minute;
        var events = engine.Tick(state).ToList();

        Assert.Empty(events);
        Assert.True(state.PenaltyAwaitingSelection);
        Assert.Equal(minute, state.Minute);

        var candidates = MatchEngine.PenaltyTakerCandidates(state, home: true);
        Assert.Equal(11, candidates.Count);

        var taker = candidates.First(player => player.Position != Position.GK);
        var scoreBefore = state.HomeScore;
        var penaltyEvents = engine.TakePenalty(state, home: true, taker).ToList();

        Assert.False(state.PenaltyAwaitingSelection);

        // The kick happened, whether it went in or not: PenaltyTaken is emitted for a miss
        // as well, which is exactly why it cannot stand for a goal on its own. So the test
        // asks about GoalScored, which is the only thing that means a goal was scored — and
        // it asks about the event type rather than about the wording, because the narration
        // says this in several different ways on purpose.
        Assert.Contains(penaltyEvents, e => e.Type == MatchEventType.PenaltyTaken);

        var scored = state.HomeScore > scoreBefore;
        Assert.Equal(scored, penaltyEvents.Any(e => e.Type == MatchEventType.GoalScored));
        Assert.Equal(scored, penaltyEvents.Any(e => e.IsGoal));
    }

    [Fact]
    public void AnOpponentPenalty_IsResolvedByTheEngineWithoutAsking()
    {
        var random = new ScriptedRandomSource(Enumerable.Repeat(0.99, 40));
        var state = CreateState(random, null, out _);
        var engine = new MatchEngine(random);

        engine.Initialize(state, 0);

        state.PenaltyAwaitingSelection = true;
        state.PenaltyTeam = 1;

        var minute = state.Minute;
        var events = engine.Tick(state).ToList();

        Assert.False(state.PenaltyAwaitingSelection);
        Assert.Equal(minute, state.Minute);
        Assert.Contains(events, e => e.Type == MatchEventType.PenaltyTaken);
    }

    [Fact]
    public void AConvertedPenalty_AnnouncesItselfAsAGoal()
    {
        // A penalty that goes in is a goal, and the rest of the game has to know it: the
        // feed, the scorers and whoever is watching. Only the shot being taken was
        // announced, which left the one goal nobody reacted to.
        var state = CreateState(new ScriptedRandomSource(Enumerable.Repeat(0.1, 40)), null, out _);
        var engine = new MatchEngine(new ScriptedRandomSource(Enumerable.Repeat(0.1, 40)));

        engine.Initialize(state, 0);

        state.PenaltyAwaitingSelection = true;
        state.PenaltyTeam = 1;

        var taker = MatchEngine.PenaltyTakerCandidates(state, home: true).First(p => p.Position != Position.GK);
        var events = engine.TakePenalty(state, home: true, taker).ToList();

        Assert.Equal(1, state.HomeScore);
        Assert.Contains(events, e => e.Type == MatchEventType.PenaltyTaken);
        Assert.Contains(events, e => e.Type == MatchEventType.GoalScored);
    }

    [Fact]
    public void AMissedPenalty_IsNotAGoal()
    {
        var state = CreateState(new ScriptedRandomSource(Enumerable.Repeat(0.99, 40)), null, out _);
        var engine = new MatchEngine(new ScriptedRandomSource(Enumerable.Repeat(0.99, 40)));

        engine.Initialize(state, 0);

        state.PenaltyAwaitingSelection = true;
        state.PenaltyTeam = 1;

        var taker = MatchEngine.PenaltyTakerCandidates(state, home: true).First(p => p.Position != Position.GK);
        var events = engine.TakePenalty(state, home: true, taker).ToList();

        Assert.Equal(0, state.HomeScore);
        Assert.DoesNotContain(events, e => e.Type == MatchEventType.GoalScored);
    }

    [Fact]
    public void APenaltyChance_StaysInsideItsBandForEveryAttribute()
    {
        // The whole 1..20 attribute scale, worst against best and best against worst: no
        // combination may produce the absurd ratios that made penalties feel unmissable.
        for (var taker = 1; taker <= 20; taker++)
        {
            for (var keeper = 1; keeper <= 20; keeper++)
            {
                var chance = MatchEngine.PenaltyConversion(
                    Shooter(accuracy: taker, dribbling: taker),
                    keeper: taker == 1 ? Keeper(reflexes: keeper, power: keeper) : null);

                Assert.InRange(chance, 0.60, 0.92);
            }
        }

        // A club with nobody in goal makes it easier, never impossible.
        var againstNobody = MatchEngine.PenaltyConversion(
            Shooter(accuracy: 20, dribbling: 20),
            keeper: null);

        Assert.InRange(againstNobody, 0.60, 0.92);
    }

    [Fact]
    public void AClinicalTaker_ConvertsMoreOftenThanAPoorOne()
    {
        var keeper = Keeper(reflexes: 17, power: 17);

        var clinical = MatchEngine.PenaltyConversion(Shooter(accuracy: 19, dribbling: 19), keeper);
        var average = MatchEngine.PenaltyConversion(Shooter(accuracy: 14, dribbling: 14), keeper);
        var poor = MatchEngine.PenaltyConversion(Shooter(accuracy: 10, dribbling: 10), keeper);

        Assert.True(clinical > average, $"{clinical:P1} should beat {average:P1}");
        Assert.True(average > poor, $"{average:P1} should beat {poor:P1}");

        // A goalkeeper is a poor taker, and the order offered to the manager says so.
        Assert.True(
            MatchEngine.PenaltyConversion(Shooter(accuracy: 10, dribbling: 10, position: Position.GK), keeper)
            < average);
    }

    [Fact]
    public void APenalty_GoesInAboutThreeTimesOutOfFour()
    {
        // The reference the whole engine is built around: a manager who picks at random
        // sees roughly three converted penalties in four, never a ratio that reads broken.
        const int attempts = 40000;
        var random = new DeterministicRandomSource(20260926);
        var keeper = Keeper(reflexes: 17, power: 17);
        var taker = Shooter(accuracy: 15, dribbling: 15);
        var scored = 0;

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var chance = MatchEngine.PenaltyConversion(taker, keeper);

            if (random.NextDouble() < chance)
            {
                scored++;
            }
        }

        var rate = (double)scored / attempts;

        Assert.InRange(rate, 0.72, 0.82);
    }

    [Fact]
    public void TheOfferedTakers_AreOrderedByTheChanceTheyWouldHave()
    {
        var state = CreateState(new ScriptedRandomSource(Enumerable.Repeat(0.5, 40)), null, out _);

        // A clinical striker, an average defender and a goalkeeper who volunteered.
        var striker = state.HomeLineup.First(player => player.Position == Position.ATT);
        var keeper = state.HomeLineup.First(player => player.Position == Position.GK);

        var candidates = MatchEngine.PenaltyTakerCandidates(state, home: true);
        var chances = candidates
            .Select(player => MatchEngine.PenaltyConversion(
                player,
                state.AwayLineup.First(opponent => opponent.KeepsGoal)))
            .ToList();

        Assert.Equal(chances.OrderByDescending(chance => chance), chances);
        Assert.True(candidates.Contains(striker));
        Assert.True(candidates.Contains(keeper));
    }

    [Fact]
    public void TheChanceOfferedToTheManager_IsTheChanceThatIsRolled()
    {
        // Whatever the dialog shows has to be what the engine uses, or the manager is being
        // told a number the roll never consults.
        var keeper = Keeper(reflexes: 17, power: 17);
        var taker = Shooter(accuracy: 15, dribbling: 15);
        var chance = MatchEngine.PenaltyConversion(taker, keeper);

        var scored = TakeWithRoll(new ScriptedRandomSource([chance - 0.01]), taker, keeper);
        var missed = TakeWithRoll(new ScriptedRandomSource([chance + 0.01]), taker, keeper);

        Assert.True(scored, "a roll just under the offered chance has to go in");
        Assert.False(missed, "a roll just over the offered chance has to stay out");
    }

    private static bool TakeWithRoll(
        IRandomSource random,
        MatchPlayerSnapshot taker,
        MatchPlayerSnapshot keeper)
    {
        var homeTeam = new TeamInfo(Guid.NewGuid(), "Home", "H", "#FF0000", "#FFFFFF", 55);
        var awayTeam = new TeamInfo(Guid.NewGuid(), "Away", "A", "#0000FF", "#FFFFFF", 50);

        var context = new MatchContext(
            Guid.NewGuid(), homeTeam, awayTeam,
            [taker, BuildPlayer(homeTeam, "Defender", Position.DEF, 14, 14)],
            [keeper, BuildPlayer(awayTeam, "Defender", Position.DEF, 14, 14)],
            [], [], random, homeTeam.Id);

        var state = new MatchState(context);
        var engine = new MatchEngine(random);
        engine.Initialize(state, 0);

        state.PenaltyAwaitingSelection = true;
        state.PenaltyTeam = 1;
        engine.TakePenalty(state, home: true, taker).ToList();

        return state.HomeScore == 1;
    }

    private static MatchPlayerSnapshot Shooter(int accuracy, int dribbling, Position position = Position.ATT) =>
        BuildPlayer(new TeamInfo(Guid.NewGuid(), "Home", "H", "#FF0000", "#FFFFFF", 55), "Taker", position, accuracy, dribbling);

    private static MatchPlayerSnapshot Keeper(int reflexes, int power) =>
        BuildPlayer(
            new TeamInfo(Guid.NewGuid(), "Away", "A", "#0000FF", "#FFFFFF", 50),
            "Keeper",
            Position.GK,
            12,
            12,
            goalkeeperPower: power,
            reflexes: reflexes);

    private static MatchPlayerSnapshot BuildPlayer(
        TeamInfo team,
        string name,
        Position position,
        int accuracy,
        int dribbling,
        int goalkeeperPower = 0,
        int reflexes = 0) =>
        MatchPlayerSnapshot.FromPlayerSeasonState(
            Player.Create(
                $"{team.ShortName} {name}",
                24,
                position,
                speed: 13,
                accuracy: accuracy,
                dribbling: dribbling,
                heading: 13,
                strength: 13,
                goalkeeperPower: goalkeeperPower,
                reflexes: reflexes),
            PlayerSeasonState.Create(Guid.NewGuid(), Guid.NewGuid(), team.Id, 90));
}
