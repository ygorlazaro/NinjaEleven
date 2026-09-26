using FootballManager.Domain.Common;
using FootballManager.Domain.Enums;
using FootballManager.Domain.Matches;
using FootballManager.Domain.Players;
using Xunit;

namespace FootballManager.Domain.Tests;

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

    private static List<MatchPlayerSnapshot> CreateLineup(TeamInfo team, Position[] positions)
    {
        var lineup = new List<MatchPlayerSnapshot>();

        for (var i = 0; i < positions.Length; i++)
        {
            var player = Player.Create(
                $"{team.ShortName} {positions[i]} {i}",
                DateOnly.FromDateTime(DateTime.Today.AddYears(-24)),
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
        keeper.SendOff();

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
    public void AnInjuredPlayerIsOutForTheRestOfTheMatch()
    {
        var random = new ScriptedRandomSource(Enumerable.Repeat(0.5, 40));
        var state = CreateState(random, null, out _);
        var engine = new MatchEngine(random);

        engine.Initialize(state, 0);

        var injured = state.HomeLineup.First(player => player.Position != Position.GK);
        injured.Injure(Injury.Light);

        Assert.False(injured.IsOnPitch);
        Assert.Equal(Injury.Light, injured.Injury);
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
        var penaltyEvents = engine.TakePenalty(state, home: true, taker).ToList();

        Assert.False(state.PenaltyAwaitingSelection);
        Assert.Contains(penaltyEvents, e => e.Type == MatchEventType.PenaltyTaken);
        Assert.True(
            state.HomeScore > 0
                ? penaltyEvents.Any(e => e.Type == MatchEventType.PenaltyTaken && e.Description.Contains("converte"))
                : penaltyEvents.Any(e => e.Type == MatchEventType.PenaltySaved)
                    || penaltyEvents.Any(e => e.Description.Contains("perde")));
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
}
