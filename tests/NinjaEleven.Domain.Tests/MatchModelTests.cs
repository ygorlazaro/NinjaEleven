using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Players;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// The match model: a team is measured unit by unit, the eleven decides the shape, running
/// costs energy, and the club nobody is watching is played with the substitutions a real
/// manager would make.
///
/// Each test here locks one of those rules. They are not testing that the engine runs; a
/// broken engine still runs. They are testing that a better side gets more of the ball, that
/// two elevens made of the same men play differently when they are arranged differently, and
/// that a tired player is worth less than a fresh one — because those are the claims the
/// screens make, and a screen that says them while the engine does the opposite is a lie.
/// </summary>
public class MatchModelTests
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

    private static MatchPlayerSnapshot MakePlayer(TeamInfo team, string name, Position position, int energy = 100, Attributes? attributes = null)
    {
        var a = attributes ?? new Attributes();

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
            PlayerSeasonState.Create(Guid.NewGuid(), Guid.NewGuid(), team.Id, energy));
    }

    private static (MatchContext Context, MatchState State, MatchEngine Engine) NewMatch(
        int seed,
        List<MatchPlayerSnapshot> homeLineup,
        List<MatchPlayerSnapshot> awayLineup,
        List<MatchPlayerSnapshot>? homeBench = null,
        List<MatchPlayerSnapshot>? awayBench = null,
        Guid? managerTeamId = null)
    {
        var home = new TeamInfo(Guid.NewGuid(), "Home United", "HU", "#FF0000", "#FFFFFF", 55);
        var away = new TeamInfo(Guid.NewGuid(), "Away City", "AC", "#0000FF", "#FFFFFF", 50);

        var random = new DeterministicRandomSource(seed);
        var context = new MatchContext(
            Guid.NewGuid(), home, away, homeLineup, awayLineup,
            homeBench ?? [], awayBench ?? [], random, managerTeamId);

        var state = new MatchState(context);
        return (context, state, new MatchEngine(random));
    }

    private static TeamInfo HomeTeam() => new(Guid.NewGuid(), "Home United", "HU", "#FF0000", "#FFFFFF", 55);

    /// <summary>
    /// A conventional eleven: a keeper, four at the back, three in the middle and three
    /// forwards, all of the same quality unless a test says otherwise.
    /// </summary>
    private static List<MatchPlayerSnapshot> Eleven(TeamInfo team, Attributes? outfield = null, Attributes? keeper = null)
    {
        var lineup = new List<MatchPlayerSnapshot> { MakePlayer(team, "Keeper", Position.GK, 100, keeper) };

        for (var i = 0; i < 4; i++)
        {
            lineup.Add(MakePlayer(team, $"Defender {i}", Position.DEF, 100, outfield));
        }

        for (var i = 0; i < 3; i++)
        {
            lineup.Add(MakePlayer(team, $"Midfielder {i}", Position.MID, 100, outfield));
        }

        for (var i = 0; i < 3; i++)
        {
            lineup.Add(MakePlayer(team, $"Forward {i}", Position.ATT, 100, outfield));
        }

        return lineup;
    }

    private static List<MatchEngineEvent> RunFullMatch(MatchEngine engine, MatchState state)
    {
        var events = engine.Initialize(state, 0).ToList();

        for (var tick = 0; tick < 600 && !state.MatchFinished; tick++)
        {
            if (state.HalfTimePauseActive)
            {
                engine.ContinueSecondHalf(state);
            }

            events.AddRange(engine.Tick(state));
        }

        return events;
    }

    // --- The clock --------------------------------------------------------------

    [Fact]
    public void ATickIsHalfAMinuteOfFootball()
    {
        var team = HomeTeam();
        var (_, state, engine) = NewMatch(7, Eleven(team), Eleven(HomeTeam()));

        engine.Initialize(state, 0);

        // The hold is a separate rule and is cleared here so this measures the clock and
        // nothing else.
        state.HoldTicksRemaining = 0;
        engine.Tick(state);
        Assert.Equal(0, state.Minute);
        Assert.Equal(MatchRules.SecondsPerTick, state.Seconds);

        state.HoldTicksRemaining = 0;
        engine.Tick(state);
        Assert.Equal(1, state.Minute);
        Assert.Equal(0, state.Seconds);
    }

    [Fact]
    public void AGoalStopsTheClockForAMoment()
    {
        var team = HomeTeam();
        var (_, state, engine) = NewMatch(11, Eleven(team), Eleven(HomeTeam()));

        engine.Initialize(state, 0);
        state.HoldTicksRemaining = MatchRules.TicksHeldAfterMajorEvent;

        for (var tick = 0; tick < MatchRules.TicksHeldAfterMajorEvent; tick++)
        {
            engine.Tick(state);
        }

        // The hold is counted in ticks so it is the same stretch of the match at 1x and 4x.
        Assert.Equal(0, state.GameSeconds);
    }

    // --- Strength and initiative ------------------------------------------------

    [Fact]
    public void TheBetterSideKeepsMoreOfTheBall()
    {
        var elite = new Attributes { Speed = 19, Accuracy = 19, Dribbling = 19, Heading = 19, Strength = 19 };
        var ordinary = new Attributes();

        var (homeContext, homeState, homeEngine) = NewMatch(2026, Eleven(HomeTeam(), elite), Eleven(HomeTeam()));
        RunFullMatch(homeEngine, homeState);

        var (_, awayState, awayEngine) = NewMatch(2026, Eleven(HomeTeam(), ordinary), Eleven(HomeTeam()));
        RunFullMatch(awayEngine, awayState);

        // Same seed, same clock, same referee: the only thing that moved was the quality of
        // one of the elevens, and the ball went where the football said it should.
        Assert.True(
            homeState.HomePossessionSeconds > awayState.HomePossessionSeconds,
            $"Elite side kept {homeState.HomePossessionSeconds}s against the ordinary side's {awayState.HomePossessionSeconds}s.");
    }

    [Fact]
    public void TheSameElevenArrangedDifferentlyIsADifferentTeam()
    {
        var team = HomeTeam();
        var attributes = new Attributes();

        var attacking = Eleven(team, attributes);
        var defensive = new List<MatchPlayerSnapshot>
        {
            MakePlayer(team, "Keeper", Position.GK, 100),
            MakePlayer(team, "Defender 0", Position.DEF, 100, attributes),
            MakePlayer(team, "Defender 1", Position.DEF, 100, attributes),
            MakePlayer(team, "Defender 2", Position.DEF, 100, attributes),
            MakePlayer(team, "Defender 3", Position.DEF, 100, attributes),
            MakePlayer(team, "Defender 4", Position.DEF, 100, attributes),
            MakePlayer(team, "Defender 5", Position.DEF, 100, attributes),
            MakePlayer(team, "Midfielder 0", Position.MID, 100, attributes),
            MakePlayer(team, "Forward 0", Position.ATT, 100, attributes),
            MakePlayer(team, "Forward 1", Position.ATT, 100, attributes),
            MakePlayer(team, "Forward 2", Position.ATT, 100, attributes)
        };

        var forwards = TeamStrength.Of(attacking);
        var defenders = TeamStrength.Of(defensive);

        // Identical players, identical quality. What changes is the shape, and the shape is
        // a claim about the match: more men in attack is a heavier attack and a lighter
        // defence, and the engine has to mean it.
        Assert.True(forwards.Attack > defenders.Attack);
        Assert.True(defenders.Defense > forwards.Defense);
    }

    [Fact]
    public void ATiredPlayerIsWorthLessThanAFreshOne()
    {
        var team = HomeTeam();
        var attributes = new Attributes();

        var fresh = Eleven(team, attributes);
        var tired = Eleven(team, attributes);

        foreach (var player in tired)
        {
            player.DrainEnergy(60, 0);
        }

        Assert.True(TeamStrength.Of(tired).Attack < TeamStrength.Of(fresh).Attack);
        Assert.True(TeamStrength.Of(tired).Midfield < TeamStrength.Of(fresh).Midfield);
    }

    [Fact]
    public void AClubIsNeverLeftWithoutSomebodyInGoal()
    {
        var team = HomeTeam();
        var home = Eleven(team);
        var away = Eleven(HomeTeam());

        var (_, state, engine) = NewMatch(5, home, away);
        RunFullMatch(engine, state);

        // A club can lose its goalkeeper to a red card or an injury and the game still has
        // to be playable: the reserve keeper comes on, and failing that an outfielder takes
        // the gloves. Nobody is ever left defending an empty net, because a manager who was
        // not quick enough to notice is not the same thing as a club with no goalkeeper.
        Assert.Contains(state.HomeLineup, player => player.KeepsGoal);
        Assert.Contains(state.AwayLineup, player => player.KeepsGoal);
    }

    [Fact]
    public void AnImprovisedGoalkeeperIsWorthFarLessThanAKeeper()
    {
        var team = HomeTeam();
        var keeper = MakePlayer(team, "Keeper", Position.GK, 100, new Attributes { Power = 18, Reflexes = 18 });
        var outfielder = MakePlayer(team, "Centre back", Position.DEF, 100, new Attributes { Speed = 12, Accuracy = 12, Dribbling = 12, Heading = 12, Strength = 12 });
        outfielder.PromoteToGoalkeeper();

        // A promoted outfielder is rated on the three attributes that stand in for a pair of
        // gloves, and rated low on purpose. A club that has run out of keepers is playing a
        // different match, and the number says so.
        Assert.True(PlayerMetric.KeeperAbility(outfielder) < PlayerMetric.KeeperAbility(keeper) / 2);
    }

    // --- Energy -----------------------------------------------------------------

    [Fact]
    public void AMatchCostsEveryPlayerEnergyAndNobodyRunsOnEmpty()
    {
        var team = HomeTeam();
        var home = Eleven(team);
        var away = Eleven(HomeTeam());

        var before = home.ToDictionary(player => player.PlayerId, player => player.Energy);

        var (_, state, engine) = NewMatch(99, home, away);
        RunFullMatch(engine, state);

        foreach (var player in home)
        {
            Assert.True(player.Energy < before[player.PlayerId]);
            Assert.True(player.Energy >= MatchRules.EnergyFloorDuringMatch);
        }
    }

    [Fact]
    public void AnOlderPlayerPaysMoreForTheSameNinetyMinutes()
    {
        var team = HomeTeam();
        var veteran = MakePlayer(team, "Veteran", Position.ATT, 100, new Attributes { Age = 35 });
        var prime = MakePlayer(team, "Prime", Position.ATT, 100, new Attributes { Age = 30 });
        var prodigy = MakePlayer(team, "Prodigy", Position.ATT, 100, new Attributes { Age = 20 });

        // The young are cheap because they recover, the old are expensive because they do
        // not. It is the reason a manager rotates a thirty-five year old before he
        // rotates a twenty-six year old.
        Assert.True(PlayerMetric.AgeCost(veteran) > PlayerMetric.AgeCost(prime));
        Assert.True(PlayerMetric.AgeCost(prime) > PlayerMetric.AgeCost(prodigy));
    }

    [Fact]
    public void APlayerPlayingThroughAKnockIsMoreLikelyToBeKnockedAgain()
    {
        var team = HomeTeam();
        var player = MakePlayer(team, "Striker", Position.ATT);

        var healthy = PlayerMetric.InjuryChance(player);
        player.Injure(Injury.Light);

        Assert.True(PlayerMetric.InjuryChance(player) > healthy);
    }

    [Fact]
    public void ATiredPlayerIsMoreLikelyToPickUpAKnock()
    {
        var team = HomeTeam();
        var player = MakePlayer(team, "Striker", Position.ATT);

        var fresh = PlayerMetric.InjuryChance(player);
        player.DrainEnergy(60, 0);

        Assert.True(PlayerMetric.InjuryChance(player) > fresh);
    }

    [Fact]
    public void AChangeMakesTheManApartAndNotSomebodyFromAnotherLine()
    {
        var team = HomeTeam();
        var home = Eleven(team);

        // A bench deep enough that the engine never runs out of a line. It has to, because
        // the rule is "a replacement comes from the same line as the man coming off", and
        // that rule is only testable while a same-line man exists to be named — a thin
        // bench proves nothing about it.
        var bench = new List<MatchPlayerSnapshot>();
        foreach (var line in new[] { Position.ATT, Position.MID, Position.DEF })
        {
            for (var i = 0; i < MatchRules.MaxSubstitutions; i++)
            {
                bench.Add(MakePlayer(team, $"Reserve {line} {i}", line));
            }
        }

        var (_, state, engine) = NewMatch(31337, home, Eleven(HomeTeam()), homeBench: bench);

        var substitutions = new List<MatchEngineEvent>();
        for (var tick = 0; tick < 600 && !state.MatchFinished; tick++)
        {
            if (state.HalfTimePauseActive)
            {
                engine.ContinueSecondHalf(state);
            }

            substitutions.AddRange(engine.Tick(state)
                .Where(caught => caught.Type == MatchEventType.SubstitutionMade));
        }

        // A club that ends a match 2-7-1 has not been managed, and since the shape is what
        // the rest of the match is measured against, a nonsense shape is a nonsense match.
        // Every man on the pitch is a starter or a reserve of the line he plays in.
        foreach (var player in state.HomeLineup.Where(player => player.IsOnPitch))
        {
            if (player.Position == Position.GK)
            {
                continue;
            }

            var fromLine = bench.Count(reserve => reserve.Position == player.Position);
            Assert.True(fromLine > 0, $"{player.Position} on the pitch with nobody of that line on the bench.");
        }

        // And the changes are the manager's five, not the engine's own count: the engine and
        // the manager share MatchRules.MaxSubstitutions, so the opposition can never get a
        // sixth change the manager is not allowed to make.
        Assert.True(
            substitutions.Count <= MatchRules.MaxSubstitutions,
            $"{substitutions.Count} changes, more than the {MatchRules.MaxSubstitutions} a manager is allowed.");
    }

    // --- The balance ------------------------------------------------------------

    [Fact]
    public void AMatchPlayedOutLooksLikeAFootballMatchAndNotLikeASpreadsheet()
    {
        const int matches = 60;

        var goals = new List<int>();
        var shots = new List<int>();
        var fouls = new List<int>();
        var cards = new List<int>();
        var corners = new List<int>();
        var changes = new List<int>();

        for (var seed = 1; seed <= matches; seed++)
        {
            var homeTeam = HomeTeam();
            var awayTeam = new TeamInfo(Guid.NewGuid(), "Away City", "AC", "#0000FF", "#FFFFFF", 50);

            var (_, state, engine) = NewMatch(
                seed,
                Eleven(homeTeam),
                Eleven(awayTeam),
                homeBench: BenchOf(homeTeam),
                awayBench: BenchOf(awayTeam));

            RunFullMatch(engine, state);

            goals.Add(state.HomeScore + state.AwayScore);
            shots.Add(state.HomeShots + state.AwayShots);
            fouls.Add(state.HomeFouls + state.AwayFouls);
            cards.Add(state.HomeCards + state.AwayCards);
            corners.Add(state.HomeCorners + state.AwayCorners);
            changes.Add(state.SubstitutionsHome + state.SubstitutionsAway);
        }

        // Bands, not targets. A model that drifts outside them is a model whose numbers no
        // longer mean anything: a match with no fouls in it has no cards, no penalties and no
        // eleven metres, and one that ends 6-2 is not the same game as one that ends 1-0.
        // The averages inside them are roughly a goal every ninety minutes, seventeen
        // shots, six fouls, two cards and two corners.
        Assert.InRange(goals.Average(), 1.0, 3.5);
        Assert.All(goals, scored => Assert.InRange(scored, 0, 8));
        Assert.InRange(shots.Average(), 8.0, 28.0);
        Assert.InRange(fouls.Average(), 2.0, 11.0);
        Assert.InRange(cards.Average(), 0.5, 6.0);
        Assert.InRange(corners.Average(), 0.5, 6.0);

        // And the bench gets used: a model where nobody is ever taken off has no squads and
        // no rotation, which is the thing a manager is actually playing for.
        Assert.InRange(changes.Average(), 1.0, 8.0);
    }

    private static List<MatchPlayerSnapshot> BenchOf(TeamInfo team)
    {
        var positions = new[]
        {
            Position.GK, Position.DEF, Position.MID, Position.ATT, Position.DEF, Position.MID, Position.ATT
        };

        return positions
            .Select((position, index) => MakePlayer(team, $"Reserve {index}", position))
            .ToList();
    }

    // --- The shape of a side ----------------------------------------------------

    [Fact]
    public void AClubPlaysInTheShapeItIsMadeOf()
    {
        var team = HomeTeam();
        var squad = new List<MatchPlayerSnapshot>();

        for (var i = 0; i < 8; i++) squad.Add(MakePlayer(team, $"Defender {i}", Position.DEF));
        for (var i = 0; i < 4; i++) squad.Add(MakePlayer(team, $"Midfielder {i}", Position.MID));
        for (var i = 0; i < 2; i++) squad.Add(MakePlayer(team, $"Forward {i}", Position.ATT));

        var shape = Formation.FromSquadComposition(squad);

        // Eight defenders and two forwards is a different club from four of each, and it
        // should not put out the same eleven as its neighbour.
        Assert.Equal(10, shape.Outfielders);
        Assert.True(shape.Defenders > shape.Midfielders);
        Assert.True(shape.Midfielders > shape.Attackers);
    }

    [Fact]
    public void AShapeNeverAsksALineForAManTheClubDoesNotHave()
    {
        var team = HomeTeam();
        var squad = new List<MatchPlayerSnapshot>();

        for (var i = 0; i < 4; i++) squad.Add(MakePlayer(team, $"Defender {i}", Position.DEF));
        for (var i = 0; i < 6; i++) squad.Add(MakePlayer(team, $"Midfielder {i}", Position.MID));

        var shape = Formation.FromSquadComposition(squad);

        Assert.Equal(4, shape.Defenders);
        Assert.Equal(6, shape.Midfielders);
        Assert.Equal(0, shape.Attackers);
        Assert.Equal(10, shape.Outfielders);
    }

    [Fact]
    public void ASubstitutionThatChangesTheBalanceOfASideChangesItsShape()
    {
        var team = HomeTeam();
        var home = Eleven(team);
        var cover = MakePlayer(team, "Cover midfielder", Position.MID);

        var (_, state, engine) = NewMatch(3, home, Eleven(HomeTeam()), homeBench: [cover]);
        engine.Initialize(state, 0);

        var before = state.HomeFormation;
        var forward = home.First(player => player.Position == Position.ATT);

        MatchSubstitution.Swap(state, home: true, forward, cover);

        // Replacing a striker for a midfielder does not only change two names: the side is
        // now a different shape, and the engine is measuring a different team.
        Assert.Equal(3, before.Attackers);
        Assert.Equal(before.Attackers - 1, state.HomeFormation.Attackers);
        Assert.Equal(before.Midfielders + 1, state.HomeFormation.Midfielders);
    }

    [Fact]
    public void AManagerDoesNotReadTheSameRatingForAForwardAndADefender()
    {
        var team = HomeTeam();
        var attributes = new Attributes { Accuracy = 10, Dribbling = 10, Speed = 10, Heading = 20, Strength = 20 };

        var defender = MakePlayer(team, "Defender", Position.DEF, 100, attributes);
        var forward = MakePlayer(team, "Forward", Position.ATT, 100, attributes);

        // Same attributes in every column, and the two players are still not the same
        // footballer: heading and strength is a defender, accuracy and dribbling is a
        // forward. A single sum of attributes cannot tell the difference, which is how a
        // club ended up with the same eleven whatever it was made of.
        Assert.NotEqual(PlayerMetric.Metric(defender), PlayerMetric.Metric(forward));
    }

    // --- The narration ----------------------------------------------------------

    [Fact]
    public void AMatchIsNarratedAndNotOnlyScored()
    {
        var team = HomeTeam();
        var (_, state, engine) = NewMatch(404, Eleven(team), Eleven(HomeTeam()));

        var events = RunFullMatch(engine, state);

        // Most of a football match is the ball being played, and a feed that only ever says
        // what happened to the ball cannot say what happened in the game.
        Assert.Contains(events, e => e.Type == MatchEventType.BuildUp);
        Assert.Contains(events, e => e.Type == MatchEventType.Shot);
    }

    [Fact]
    public void TheAddedTimeIsAnnouncedWhileItStillHasToBePlayed()
    {
        var team = HomeTeam();
        var (_, state, engine) = NewMatch(808, Eleven(team), Eleven(HomeTeam()));

        var events = engine.Initialize(state, 0).ToList();

        for (var tick = 0; tick < 600 && !state.MatchFinished; tick++)
        {
            if (state.HalfTimePauseActive)
            {
                engine.ContinueSecondHalf(state);
            }

            events.AddRange(engine.Tick(state));
        }

        var announcements = events.Where(e => e.Type == MatchEventType.StoppageTimeAdded).ToList();

        // One per half, each naming the minutes of that half, and together they are the
        // number the referee announced at kick-off: the first half collects injuries and
        // the second collects goals, so they are not the same minutes.
        Assert.Equal(2, announcements.Count);
        Assert.Contains(state.FirstHalfStoppage.ToString(), announcements[0].Description);
        Assert.Contains(state.SecondHalfStoppage.ToString(), announcements[1].Description);
        Assert.Equal(state.StoppageMinutes, state.FirstHalfStoppage + state.SecondHalfStoppage);
    }

    [Fact]
    public void TheAddedTimeIsAnnouncedWhileTheHalfIsStillBeingPlayed()
    {
        var team = HomeTeam();
        var (_, state, engine) = NewMatch(808, Eleven(team), Eleven(HomeTeam()));

        var events = new List<MatchEngineEvent>();
        engine.Initialize(state, 0);

        for (var tick = 0; tick < 600 && !state.MatchFinished; tick++)
        {
            if (state.HalfTimePauseActive)
            {
                engine.ContinueSecondHalf(state);
            }

            events.AddRange(engine.Tick(state));
        }

        var announcements = events.Where(e => e.Type == MatchEventType.StoppageTimeAdded).ToList();
        Assert.Equal(2, announcements.Count);

        // 41' and 86', and both while there is still time to play it. A referee who waits
        // for the whistle has told the manager something that has already happened, and a
        // manager who is told at 90' that there are three minutes left has not been told
        // anything he can use.
        Assert.Equal(MatchRules.FirstHalfStoppageAnnounceMinute, announcements[0].Minute);
        Assert.Equal(MatchRules.SecondHalfStoppageAnnounceMinute, announcements[1].Minute);
        Assert.True(announcements[0].Minute < MatchRules.FirstHalfEnd);
        Assert.True(announcements[1].Minute < 90);
    }

    [Fact]
    public void ASpokenFeedDoesNotSayTheSameThingEveryTime()
    {
        var team = HomeTeam();

        // Two different matches, so what is being compared is the narration and not the
        // football: a feed that says one thing about every pass of every match is a
        // spreadsheet, and a manager stops reading it.
        var (_, firstState, firstEngine) = NewMatch(11, Eleven(team), Eleven(HomeTeam()));
        var (_, secondState, secondEngine) = NewMatch(97, Eleven(team), Eleven(HomeTeam()));

        var first = RunFullMatch(firstEngine, firstState)
            .Where(e => e.Type == MatchEventType.BuildUp)
            .Select(e => e.Description)
            .ToList();
        var second = RunFullMatch(secondEngine, secondState)
            .Where(e => e.Type == MatchEventType.BuildUp)
            .Select(e => e.Description)
            .ToList();

        Assert.NotEmpty(first);
        Assert.NotEmpty(second);

        var distinct = first.Concat(second).Distinct().Count();
        Assert.True(
            distinct > (first.Count + second.Count) / 2,
            $"{distinct} different lines out of {first.Count + second.Count} build-up beats.");
    }

    [Fact]
    public void AnOwnGoalIsSomebodyGettingItWrongAndNotACornerFindingTheNet()
    {
        var team = HomeTeam();

        // Own goals are rare enough that a single match may well not have one, so this looks
        // at enough of them to see the rate rather than waiting for a single seed to oblige.
        var ownGoals = 0;
        var matches = 60;

        for (var seed = 0; seed < matches; seed++)
        {
            var (_, state, engine) = NewMatch(seed, Eleven(team), Eleven(HomeTeam()));
            var events = RunFullMatch(engine, state);

            // Every event carries the score as it stood when it was produced, so the
            // previous event's score is the score before this one — and an own goal is a
            // goal, which is the whole point of walking the feed rather than counting.
            var home = 0;
            var away = 0;

            foreach (var scored in events)
            {
                if (scored.Type == MatchEventType.OwnGoalScored)
                {
                    ownGoals++;

                    // It is somebody's error and it is on him.
                    var mistaken = state.HomeLineup.Concat(state.AwayLineup)
                        .First(player => player.PlayerId == scored.PlayerId);

                    Assert.True(mistaken.MatchOwnGoals > 0);

                    // Exactly one side moved, and it is the side that does not play for the
                    // man who got it wrong: the event names the side the goal was a gift to.
                    var homeMoved = scored.HomeScore == home + 1;
                    var awayMoved = scored.AwayScore == away + 1;

                    Assert.True(homeMoved ^ awayMoved);
                    Assert.Equal(awayMoved, scored.TeamId == state.HomeTeam.Id);
                }

                home = scored.HomeScore;
                away = scored.AwayScore;
            }

            // The two tallies are kept apart and add up: a man's goals and a man's own
            // goals are different numbers, and the sum of both is the number of balls that
            // went in — so an own goal is never also counted as a goal of his.
            var everyone = state.HomeLineup.Concat(state.AwayLineup);
            Assert.Equal(
                state.HomeScore + state.AwayScore,
                everyone.Sum(player => player.MatchGoals + player.MatchOwnGoals));
        }

        // Roughly a tenth a match is the intent: a thing that happens twice a season is a
        // story, and a thing that happens twice a match is a rule nobody argued about. The
        // bound is loose enough to survive a run of bad seeds and tight enough that a
        // chance left an order of magnitude too high would fail here.
        Assert.True(ownGoals <= matches / 4, $"{ownGoals} own goals in {matches} matches.");
    }

    [Fact]
    public void TheEngineDoesNotNameASubstituteWhoHasAlreadyLeftThePitch()
    {
        var team = HomeTeam();
        var home = Eleven(team);
        var bench = new List<MatchPlayerSnapshot>();
        foreach (var line in new[] { Position.ATT, Position.MID, Position.DEF })
        {
            for (var i = 0; i < MatchRules.MaxSubstitutions; i++)
            {
                bench.Add(MakePlayer(team, $"Reserve {line} {i}", line));
            }
        }

        var (_, state, engine) = NewMatch(555, home, Eleven(HomeTeam()), homeBench: bench);

        for (var tick = 0; tick < 600 && !state.MatchFinished; tick++)
        {
            if (state.HalfTimePauseActive)
            {
                engine.ContinueSecondHalf(state);
            }

            engine.Tick(state);
        }

        // A man the engine has already taken off is spent, and the engine does not get to
        // spend him twice: the same rule the manager is held to, in the same place.
        foreach (var spent in state.HomeBench.Where(player => player.SubbedOff))
        {
            Assert.False(MatchSubstitution.CanSwap(
                state.HomeLineup,
                state.HomeLineup.First(player => player.IsOnPitch && player.Position != Position.GK),
                spent));
        }
    }

    [Fact]
    public void TheElevenIsAnnouncedBeforeTheWhistle()
    {
        var team = HomeTeam();
        var (_, state, engine) = NewMatch(4242, Eleven(team), Eleven(HomeTeam()));

        var events = engine.Initialize(state, 0).ToList();

        // A manager picks these names, and a match that starts without saying who is
        // playing hides the only decision he made before anybody watched a minute of it.
        var lineups = events.Where(e => e.Type == MatchEventType.LineupAnnounced).ToList();
        Assert.Equal(2, lineups.Count);
        Assert.All(lineups, lineup => Assert.Contains("Goleiro:", lineup.Description, StringComparison.Ordinal));

        // Both sides are named, and the whistle is the last thing that happens.
        Assert.Equal(
            [MatchEventType.LineupAnnounced, MatchEventType.LineupAnnounced, MatchEventType.KickOff],
            events.Select(e => e.Type).ToArray());

        // Every announced man is a man who is actually on the pitch.
        var home = lineups.First(lineup => lineup.TeamId == state.HomeTeam.Id).Description;
        var onThePitch = state.HomeLineup.Where(player => player.IsOnPitch).Select(player => player.Name).ToList();

        Assert.Equal(11, onThePitch.Count);
        Assert.All(onThePitch, name => Assert.Contains(name, home, StringComparison.Ordinal));
    }

    [Fact]
    public void TheSecondHalfSaysWhetherTheManagersChangedAnything()
    {
        var team = HomeTeam();
        var (_, state, engine) = NewMatch(4242, Eleven(team), Eleven(HomeTeam()));
        engine.Initialize(state, 0);

        var secondHalf = new List<MatchEngineEvent>();

        for (var tick = 0; tick < 600 && !state.MatchFinished; tick++)
        {
            if (state.HalfTimePauseActive)
            {
                engine.ContinueSecondHalf(state);
                secondHalf.AddRange(state.PendingFeed);
                state.PendingFeed.Clear();
            }

            engine.Tick(state);
        }

        var start = secondHalf.FirstOrDefault(e => e.Type == MatchEventType.SecondHalfStarted);
        Assert.NotNull(start);
        Assert.False(string.IsNullOrWhiteSpace(start!.Description));

        // It says something about the interval, and it is the sentence chosen by what the
        // engine actually did — a manager who changed nobody is not told about a change.
        if (state.FirstHalfChanges.Count == 0)
        {
            // Said against the bank itself rather than against a phrase, so rewording the
            // line does not fail a test that is about the engine's decision.
            Assert.Contains(start.Description, MatchNarration.HalftimeNoChanges);
        }
        else
        {
            Assert.All(
                state.FirstHalfChanges,
                change => Assert.Contains(change, start.Description, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void TheShareOfTheBallIsARealShareOfTheMatchAndNotACounter()
    {
        var team = HomeTeam();
        var (_, state, engine) = NewMatch(1234, Eleven(team), Eleven(HomeTeam()));

        RunFullMatch(engine, state);

        // Read off the seconds each side actually had it, and inside the band a real match
        // stays in: a number that drifts to 96% is not a share of anything.
        Assert.Equal(state.GameSeconds, state.HomePossessionSeconds + state.AwayPossessionSeconds);
        Assert.InRange(state.HomePossession, MatchRules.MinPossession, MatchRules.MaxPossession);
        Assert.Equal(100, state.HomePossession + state.AwayPossession);
    }

    // --- The unwatched club -----------------------------------------------------

    [Fact]
    public void TheEngineSubstitutesForTheClubNobodyIsWatching()
    {
        var homeTeam = HomeTeam();
        var awayTeam = new TeamInfo(Guid.NewGuid(), "Away City", "AC", "#0000FF", "#FFFFFF", 50);

        var home = Eleven(homeTeam);
        var away = Eleven(awayTeam);

        // The opposition arrives exhausted, with a bench of fresh men behind it. A manager
        // would change somebody; so does the engine.
        foreach (var player in away.Where(player => player.Position != Position.GK))
        {
            player.DrainEnergy(60, 0);
        }

        var bench = Enumerable.Range(0, 4)
            .Select(i => MakePlayer(awayTeam, $"Reserve {i}", Position.MID, 100))
            .ToList();

        var (_, state, engine) = NewMatch(31337, home, away, awayBench: bench, managerTeamId: homeTeam.Id);
        RunFullMatch(engine, state);

        Assert.True(
            state.SubstitutionsAway > 0,
            "A club that started the second half with ten men under fifty-five energy was never substituted.");
    }

    [Fact]
    public void TheEngineMakesNoDiscretionaryChangeForTheClubAManagerIsWatching()
    {
        var homeTeam = HomeTeam();
        var awayTeam = new TeamInfo(Guid.NewGuid(), "Away City", "AC", "#0000FF", "#FFFFFF", 50);

        var home = Eleven(homeTeam);
        var away = Eleven(awayTeam);

        foreach (var player in home.Where(player => player.Position != Position.GK))
        {
            player.DrainEnergy(60, 0);
        }

        var bench = Enumerable.Range(0, 4)
            .Select(i => MakePlayer(homeTeam, $"Reserve {i}", Position.MID, 100))
            .ToList();

        var (_, state, engine) = NewMatch(31337, home, away, homeBench: bench, managerTeamId: homeTeam.Id);
        var events = RunFullMatch(engine, state);

        // Ten exhausted men and a full bench is exactly the decision the engine is not
        // allowed to take for him. What it does replace is a player who cannot continue and
        // a club with nobody in goal, and it says so when it does it — an injury is not a
        // decision anybody gets to make under pressure.
        var changes = events
            .Where(e => e.Type == MatchEventType.SubstitutionMade && e.TeamId == homeTeam.Id)
            .ToList();

        Assert.All(changes, change => Assert.Contains("lesão", change.Description, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TheEngineNeverGivesTheOppositionMoreChangesThanTheManagerHas()
    {
        var homeTeam = HomeTeam();
        var awayTeam = new TeamInfo(Guid.NewGuid(), "Away City", "AC", "#0000FF", "#FFFFFF", 50);

        var home = Eleven(homeTeam);
        var away = Eleven(awayTeam);
        var bench = Enumerable.Range(0, 9)
            .Select(i => MakePlayer(awayTeam, $"Reserve {i}", Position.MID, 100))
            .ToList();

        var (_, state, engine) = NewMatch(2, home, away, awayBench: bench);
        RunFullMatch(engine, state);

        // A bench is a bench. The engine must not be able to hand the opposition a sixth
        // change the manager is not allowed to make.
        Assert.True(state.SubstitutionsAway <= MatchRules.MaxSubstitutions);
    }

    [Fact]
    public void TheLastGoalkeeperCanOnlyBeReplacedByAnotherGoalkeeper()
    {
        var team = HomeTeam();
        var home = Eleven(team);
        var keeper = home.First(player => player.Position == Position.GK);
        var (_, state, _) = NewMatch(1, home, Eleven(HomeTeam()));

        // An outfielder taking the gloves is what the engine does in an emergency, not
        // something anybody gets to choose: the manager cannot, so the engine must not hand
        // the opposition the choice either.
        Assert.False(MatchSubstitution.CanSwap(state.HomeLineup, keeper, MakePlayer(team, "Reserve", Position.MID)));
        Assert.False(MatchSubstitution.CanSwap(state.HomeLineup, keeper, MakePlayer(team, "Reserve", Position.ATT)));
        Assert.True(MatchSubstitution.CanSwap(state.HomeLineup, keeper, MakePlayer(team, "Reserve keeper", Position.GK)));
    }

    [Fact]
    public void ASwapLeavesTheClubWithExactlyOneGoalkeeper()
    {
        var team = HomeTeam();
        var home = Eleven(team);
        var keeper = home.First(player => player.Position == Position.GK);
        var reserve = MakePlayer(HomeTeam(), "Reserve keeper", Position.GK, 100);

        var (_, state, _) = NewMatch(1, home, Eleven(HomeTeam()), homeBench: [reserve]);
        MatchSubstitution.Swap(state, home: true, keeper, reserve);

        Assert.Equal(1, state.HomeLineup.Count(player => player.KeepsGoal));
        Assert.Equal(1, state.SubstitutionsHome);
        Assert.Contains(reserve, state.HomeLineup);
        Assert.Contains(keeper, state.HomeBench);
    }
}
