using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Players;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// A serious injury is the one moment in a match the manager has to act on, and the rules
/// here are about who decides and what the clock does until he does.
///
/// A knock is drawn, so these tests look for the seed that produces the case rather than
/// forcing a roll. The search is over a fixed range of seeds, so the seed each test settles
/// on is the same one every run — the same trick the determinism test uses.
/// </summary>
public class InjuryDecisionTests
{
    private sealed class Attributes
    {
        public int Speed { get; init; } = 12;
        public int Accuracy { get; init; } = 12;
        public int Dribbling { get; init; } = 12;
        public int Heading { get; init; } = 12;
        public int Strength { get; init; } = 12;
        public int Power { get; init; } = 18;
        public int Reflexes { get; init; } = 18;
        public int Age { get; init; } = 24;
    }

    private static MatchPlayerSnapshot MakePlayer(TeamInfo team, string name, Position position)
    {
        var a = new Attributes();

        return MatchPlayerSnapshot.FromPlayerSeasonState(
            Player.Create(
                name,
                a.Age,
                position,
                speed: a.Speed,
                accuracy: a.Accuracy,
                dribbling: a.Dribbling,
                heading: a.Heading,
                strength: a.Strength,
                goalkeeperPower: position == Position.GK ? a.Power : 0,
                reflexes: position == Position.GK ? a.Reflexes : 0),
            PlayerSeasonState.Create(Guid.NewGuid(), Guid.NewGuid(), team.Id, 100));
    }

    private static List<MatchPlayerSnapshot> Eleven(TeamInfo team, string prefix)
    {
        var lineup = new List<MatchPlayerSnapshot> { MakePlayer(team, $"{prefix} Keeper", Position.GK) };

        for (var i = 0; i < 4; i++)
        {
            lineup.Add(MakePlayer(team, $"{prefix} Defender {i}", Position.DEF));
        }

        for (var i = 0; i < 3; i++)
        {
            lineup.Add(MakePlayer(team, $"{prefix} Midfielder {i}", Position.MID));
        }

        for (var i = 0; i < 3; i++)
        {
            lineup.Add(MakePlayer(team, $"{prefix} Forward {i}", Position.ATT));
        }

        return lineup;
    }

    private static List<MatchPlayerSnapshot> Bench(TeamInfo team, string prefix) =>
    [
        MakePlayer(team, $"{prefix} Reserve 0", Position.MID),
        MakePlayer(team, $"{prefix} Reserve 1", Position.DEF),
        MakePlayer(team, $"{prefix} Reserve 2", Position.ATT)
    ];

    private sealed record Running(
        MatchState State,
        MatchEngine Engine,
        TeamInfo Home,
        TeamInfo Away,
        Guid ManagerTeamId);

    private static Running NewMatch(int seed, bool withManager)
    {
        var home = new TeamInfo(Guid.NewGuid(), "Home United", "HU", "#FF0000", "#FFFFFF", 55);
        var away = new TeamInfo(Guid.NewGuid(), "Away City", "AC", "#0000FF", "#FFFFFF", 50);
        var random = new DeterministicRandomSource(seed);

        var context = new MatchContext(
            Guid.NewGuid(),
            home,
            away,
            Eleven(home, "H"),
            Eleven(away, "A"),
            Bench(home, "H"),
            Bench(away, "A"),
            random,
            withManager ? home.Id : null);

        var state = new MatchState(context);
        var engine = new MatchEngine(random);
        engine.Initialize(state, 0);

        return new Running(state, engine, home, away, home.Id);
    }

    private const int SeedSearch = 400;

    /// <summary>
    /// Runs a match until the manager's own club has a man who cannot carry on, and hands
    /// back the match stopped at that point. Returns null when no seed in the range produces
    /// the case, which would mean the engine is not asking at all.
    /// </summary>
    private static Running? FindHeldMatch(bool withManager = true)
    {
        for (var seed = 1; seed <= SeedSearch; seed++)
        {
            var running = NewMatch(seed, withManager);

            for (var tick = 0; tick < 400 && !running.State.MatchFinished; tick++)
            {
                if (running.State.HalfTimePauseActive)
                {
                    running.Engine.ContinueSecondHalf(running.State);
                }

                if (running.State.InjuryAwaitingSubstitution)
                {
                    return running;
                }

                running.Engine.Tick(running.State);
            }
        }

        return null;
    }

    [Fact]
    public void ASevereInjuryToTheManagersOwnClubStopsTheMatchAndNamesThePlayer()
    {
        var running = FindHeldMatch();

        Assert.NotNull(running);
        var state = running!.State;

        Assert.Equal(running.Home.Id, state.ManagerTeamId);
        Assert.NotNull(state.InjuryPlayerId);
        Assert.Equal(1, state.InjuryTeam);
        Assert.InRange(state.PendingInjuryMatchesOut, MatchRules.MinMatchesOutSevere, MatchRules.MaxMatchesOutSevere - 1);
    }

    [Fact]
    public void TheHurtPlayerIsStillOnThePitchUntilTheChangeIsMade()
    {
        var running = FindHeldMatch();
        Assert.NotNull(running);

        var state = running!.State;
        var hurt = state.HomeLineup.Single(player => player.PlayerId == state.InjuryPlayerId);

        // He is not off yet, because the change that takes him off is the change the
        // manager has not made. Marking him off here is what would leave a substitution
        // with nobody to be made for.
        Assert.True(hurt.IsOnPitch);
        Assert.Equal(Injury.None, hurt.Injury);
        Assert.Equal(11, state.HomeLineup.Count);
    }

    [Fact]
    public void TheClockDoesNotMoveWhileTheManagerHasNotAnswered()
    {
        var running = FindHeldMatch();
        Assert.NotNull(running);

        var state = running!.State;
        var minute = state.Minute;
        var second = state.Seconds;

        for (var tick = 0; tick < 20; tick++)
        {
            Assert.Empty(running.Engine.Tick(state));
        }

        Assert.Equal(minute, state.Minute);
        Assert.Equal(second, state.Seconds);
        Assert.True(state.InjuryAwaitingSubstitution);
    }

    [Fact]
    public void AManagerWhoNeverAnswersIsReplacedByTheEngine()
    {
        // The clock is held for a manager who claimed the match. A manager who closes the tab
        // leaves it held for ever, and the world behind it stops: the fixture stays owed and
        // the window never closes. So the engine answers the question itself, exactly as it
        // does for a match nobody ever claimed.
        var running = FindHeldMatch();
        Assert.NotNull(running);

        var state = running!.State;
        Assert.True(state.InjuryAwaitingSubstitution);

        var outgoing = state.HomeLineup.Single(player => player.PlayerId == state.InjuryPlayerId);
        var matchesOut = state.PendingInjuryMatchesOut;

        var events = running.Engine.ReleaseTheManager(state);

        // The wait is over, the man who cannot carry on is off, and the eleven under the
        // scoreboard is a whole eleven rather than a club one man short and standing still.
        // Which man the engine sent on is its own business — the point is that it sent one,
        // so it is not asserted which: a test that named the replacement would break the day
        // the engine's own choice improved.
        Assert.False(state.InjuryAwaitingSubstitution);
        Assert.Null(state.InjuryPlayerId);
        Assert.True(outgoing.InjuredOff);
        Assert.Equal(matchesOut, outgoing.InjuryMatchesOut);
        Assert.DoesNotContain(outgoing, state.HomeLineup);
        Assert.Equal(11, state.HomeLineup.Count);
        Assert.NotEmpty(events);
    }

    [Fact]
    public void AClaimIsOnlyHandedBackWhenThereIsSomethingWaitingOnIt()
    {
        // A manager who is watching a match that is not waiting on him is left alone. The
        // whole point of the claim was that he is there, and taking it away from him because
        // he is thinking would rob him of the very decisions the claim was for.
        var running = FindHeldMatch();
        Assert.NotNull(running);

        var state = running!.State;
        var hurt = state.HomeLineup.Single(player => player.PlayerId == state.InjuryPlayerId);
        state.ResolvePendingInjury(hurt);

        Assert.False(state.InjuryAwaitingSubstitution);
        Assert.Empty(running.Engine.ReleaseTheManager(state));
    }

    [Fact]
    public void NamingTheReplacementEndsTheWaitAndRecordsTheInjury()
    {
        var running = FindHeldMatch();
        Assert.NotNull(running);

        var state = running!.State;
        var outgoing = state.HomeLineup.Single(player => player.PlayerId == state.InjuryPlayerId);
        var matchesOut = state.PendingInjuryMatchesOut;
        var incoming = state.HomeBench.First(player => !player.SubbedOff);

        MatchSubstitution.Swap(state, home: true, outgoing, incoming);
        state.ResolvePendingInjury(outgoing);

        Assert.False(state.InjuryAwaitingSubstitution);
        Assert.Null(state.InjuryPlayerId);
        Assert.Null(state.InjuryTeam);
        Assert.Equal(0, state.PendingInjuryMatchesOut);

        Assert.True(outgoing.InjuredOff);
        Assert.Equal(Injury.Grave, outgoing.Injury);
        Assert.Equal(matchesOut, outgoing.InjuryMatchesOut);
    }

    [Fact]
    public void TheClockRunsAgainOnceTheChangeIsMade()
    {
        var running = FindHeldMatch();
        Assert.NotNull(running);

        var state = running!.State;
        var outgoing = state.HomeLineup.Single(player => player.PlayerId == state.InjuryPlayerId);
        var incoming = state.HomeBench.First(player => !player.SubbedOff);

        MatchSubstitution.Swap(state, home: true, outgoing, incoming);
        state.ResolvePendingInjury(outgoing);

        var before = state.Sequence;
        running.Engine.Tick(state);

        Assert.True(state.Sequence > before);
    }

    [Fact]
    public void ASubstitutionForSomebodyElseDoesNotSettleTheInjury()
    {
        var running = FindHeldMatch();
        Assert.NotNull(running);

        var state = running!.State;
        var hurt = state.InjuryPlayerId;
        var someOtherPlayer = state.HomeLineup.First(player => player.PlayerId != hurt);
        var incoming = state.HomeBench.First(player => !player.SubbedOff);

        MatchSubstitution.Swap(state, home: true, someOtherPlayer, incoming);
        state.ResolvePendingInjury(someOtherPlayer);

        // The match is still waiting on the man who cannot carry on.
        Assert.True(state.InjuryAwaitingSubstitution);
        Assert.Equal(hurt, state.InjuryPlayerId);
    }

    [Fact]
    public void TheEngineCoversTheAbsenceOfAClubNobodyIsWatching()
    {
        var covered = false;

        for (var seed = 1; seed <= SeedSearch && !covered; seed++)
        {
            var running = NewMatch(seed, withManager: false);
            var state = running.State;

            for (var tick = 0; tick < 400 && !state.MatchFinished && !covered; tick++)
            {
                if (state.HalfTimePauseActive)
                {
                    running.Engine.ContinueSecondHalf(state);
                }

                running.Engine.Tick(state);

                // Nobody is asked, ever: there is no manager in this match to ask.
                Assert.False(state.InjuryAwaitingSubstitution);

                var everybody = state.HomeLineup
                    .Concat(state.AwayLineup)
                    .Concat(state.HomeBench)
                    .Concat(state.AwayBench);

                // A man who went off hurt, and somebody on the pitch who came on for him:
                // the engine dealt with it in the same breath rather than holding the match
                // on a decision nobody is there to make.
                covered = everybody.Any(player => player.Injury == Injury.Grave && player.InjuredOff)
                    && everybody.Any(player => player.SubbedIn);
            }
        }

        Assert.True(covered, "No seed in the range produced a covered absence.");
    }

    [Fact]
    public void AManagerIsNeverAskedAboutAMatchHeIsNotIn()
    {
        for (var seed = 1; seed <= 60; seed++)
        {
            var running = NewMatch(seed, withManager: false);

            for (var tick = 0; tick < 400 && !running.State.MatchFinished; tick++)
            {
                if (running.State.HalfTimePauseActive)
                {
                    running.Engine.ContinueSecondHalf(running.State);
                }

                running.Engine.Tick(running.State);

                Assert.False(running.State.InjuryAwaitingSubstitution);
            }
        }
    }

    [Fact]
    public void ALightKnoughPlayerKeepsPlayingAndNobodyIsHeld()
    {
        var found = false;

        for (var seed = 1; seed <= SeedSearch && !found; seed++)
        {
            var running = NewMatch(seed, withManager: true);
            var state = running.State;

            for (var tick = 0; tick < 400 && !state.MatchFinished && !found; tick++)
            {
                if (state.HalfTimePauseActive)
                {
                    running.Engine.ContinueSecondHalf(state);
                }

                running.Engine.Tick(state);

                var light = state.HomeLineup.Concat(state.AwayLineup)
                    .FirstOrDefault(player => player.Injury == Injury.Light);

                if (light is null)
                {
                    continue;
                }

                found = true;

                // A player who is hurt but standing is still playing: nobody was sent off,
                // and the match is not waiting for anybody to be named.
                Assert.True(light.IsOnPitch);
                Assert.False(state.InjuryAwaitingSubstitution);
            }
        }

        Assert.True(found, "No seed in the range produced a player playing through a knock.");
    }

    [Fact]
    public void AClubWithNobodyOnTheBenchIsNotHeldOnAQuestionItCannotAnswer()
    {
        for (var seed = 1; seed <= 60; seed++)
        {
            var home = new TeamInfo(Guid.NewGuid(), "Home United", "HU", "#FF0000", "#FFFFFF", 55);
            var away = new TeamInfo(Guid.NewGuid(), "Away City", "AC", "#0000FF", "#FFFFFF", 50);
            var random = new DeterministicRandomSource(seed);

            // A home bench with nobody who could be sent on: the eleven is all there is.
            var context = new MatchContext(
                Guid.NewGuid(),
                home,
                away,
                Eleven(home, "H"),
                Eleven(away, "A"),
                [],
                Bench(away, "A"),
                random,
                home.Id);

            var state = new MatchState(context);
            var engine = new MatchEngine(random);
            engine.Initialize(state, 0);

            for (var tick = 0; tick < 400 && !state.MatchFinished; tick++)
            {
                if (state.HalfTimePauseActive)
                {
                    engine.ContinueSecondHalf(state);
                }

                engine.Tick(state);

                // Held on a decision, with no man available to make it: the match would
                // never finish. So the engine covers it and carries on.
                Assert.False(state.InjuryAwaitingSubstitution);
            }
        }
    }
}
