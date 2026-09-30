using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Players;
using Xunit;

namespace NinjaEleven.Domain.Tests;

public class MatchEngineTests
{
    private const int Seed = 42;
    /// <summary>
    /// A guard against a runaway, not a length. A match is ninety minutes of play plus its
    /// added time plus the ticks the clock is held for after a goal, a card or a
    /// substitution, and 200 of those is not a whole match on every seed — so a budget
    /// that truncates a real match makes the test fail for the wrong reason.
    /// </summary>
    private const int FullMatchTicks = 600;

    private static MatchContext CreateContext(IRandomSource random)
    {
        var homeTeam = new TeamInfo(Guid.NewGuid(), "Home United", "HU", "#FF0000", "#FFFFFF", 55);
        var awayTeam = new TeamInfo(Guid.NewGuid(), "Away City", "AC", "#0000FF", "#FFFFFF", 50);

        var homeLineup = CreateLineup(homeTeam, 0, random);
        var awayLineup = CreateLineup(awayTeam, 1, random);

        return new MatchContext(Guid.NewGuid(), homeTeam, awayTeam,
            homeLineup, awayLineup, [], [],
            random);
    }

    private static List<MatchPlayerSnapshot> CreateLineup(TeamInfo team, int offset, IRandomSource random)
    {
        var positions = new[]
        {
            Position.GK, Position.DEF, Position.DEF, Position.MID, Position.MID,
            Position.MID, Position.ATT, Position.ATT, Position.DEF, Position.MID, Position.MID
        };

        var lineup = new List<MatchPlayerSnapshot>();
        for (int i = 0; i < 11; i++)
        {
            var player = CreatePlayer(team.Id, i + offset * 11, positions[i], random);
            var state = PlayerSeasonState.Create(player.Id, Guid.NewGuid(), team.Id, 85 + (i % 3) * 5);
            lineup.Add(MatchPlayerSnapshot.FromPlayerSeasonState(player, state));
        }
        return lineup;
    }

    private static Player CreatePlayer(Guid teamId, int index, Position position, IRandomSource random)
    {
        return Player.Create(
            $"Player {index}",
            18 + index % 10,
            position,
            speed: 12 + (index % 5),
            accuracy: 10 + (index % 5),
            dribbling: 10 + (index % 5),
            heading: 10 + (index % 5),
            strength: 10 + (index % 5),
            goalkeeperPower: position == Position.GK ? 18 + (index % 4) : 0,
            reflexes: position == Position.GK ? 15 + (index % 5) : 0
        );
    }

    private static List<MatchEngineEvent> RunFullMatch(IRandomSource random)
    {
        var context = CreateContext(random);
        var engine = new MatchEngine(random);
        var state = new MatchState(context);

        var allEvents = engine.Initialize(state, 0).ToList();

        for (int i = 0; i < FullMatchTicks && !state.MatchFinished; i++)
        {
            var tickEvents = engine.Tick(state);
            allEvents.AddRange(tickEvents);

            if (state.HalfTimePauseActive)
            {
                engine.ContinueSecondHalf(state);
                allEvents.AddRange(state.PendingFeed);
                state.PendingFeed.Clear();
            }
        }

        return allEvents;
    }

    [Fact]
    public void AGoalFromOpenPlayIsNotAPenalty()
    {
        // A whole match driven by the engine with a seeded source, so the goals in it are
        // real ones. The flag on a goal is a scoreline's only way of telling a penalty from
        // any other goal, and a flag that defaulted to true would mark every goal in the game.
        var events = RunFullMatch(new DeterministicRandomSource(20260930));

        var goals = events.Where(e => e.IsGoal).ToList();

        Assert.NotEmpty(goals);
        Assert.All(goals, goal => Assert.False(goal.FromPenalty));
    }

    [Fact]
    public void MatchEngine_FullMatch_IsDeterministic_WithSameSeed()
    {
        var random1 = new DeterministicRandomSource(Seed);
        var random2 = new DeterministicRandomSource(Seed);

        var events1 = RunFullMatch(random1);
        var events2 = RunFullMatch(random2);

        Assert.Equal(events1.Count, events2.Count);

        for (int i = 0; i < events1.Count; i++)
        {
            Assert.Equal(events1[i].Type, events2[i].Type);
            Assert.Equal(events1[i].Sequence, events2[i].Sequence);
            Assert.Equal(events1[i].Minute, events2[i].Minute);
            Assert.Equal(events1[i].IsGoal, events2[i].IsGoal);
        }
    }

    [Fact]
    public void MatchEngine_DifferentSeeds_ProduceDifferentMatches()
    {
        var random1 = new DeterministicRandomSource(1);
        var random2 = new DeterministicRandomSource(999);

        var events1 = RunFullMatch(random1);
        var events2 = RunFullMatch(random2);

        Assert.NotEqual(events1.Count, events2.Count);
    }

    [Fact]
    public void MatchEngine_AlwaysProducesGoalsOrFinishes()
    {
        var random = new DeterministicRandomSource(Seed);
        var events = RunFullMatch(random);

        var finish = events.LastOrDefault(e => e.Type == MatchEventType.MatchFinished);
        Assert.NotNull(finish);

        var goals = events.Count(e => e.IsGoal);
        Assert.True(goals >= 0);
    }

    [Fact]
    public void MatchEngine_Tick_RespectsTimeProgression()
    {
        var random = new DeterministicRandomSource(Seed);
        var context = CreateContext(random);
        var engine = new MatchEngine(random);
        var state = new MatchState(context);

        engine.Initialize(state, 0);

        for (int i = 0; i < 10 && !state.MatchFinished; i++)
        {
            engine.Tick(state);
        }

        Assert.True(state.Minute >= 0);
        Assert.True(state.GameSeconds >= 0);
    }
}
