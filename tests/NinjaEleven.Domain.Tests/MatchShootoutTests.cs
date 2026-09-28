using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Players;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// A cup tie that is level after ninety minutes goes to penalties, and the match is not
/// over until the last one has been taken.
///
/// This is the seam between the ninety minutes and the shootout, and it used to not exist:
/// the engine finished every match at ninety, and the tie was decided afterwards by a
/// number drawn out of a loop, so nothing that happened in a shootout had ever been on a
/// screen. The three facts these tests hold down are that a second leg decides by the
/// aggregate rather than by the ninety minutes, that a championship match never does this at
/// all, and that the manager's club decides the order of its own five before anybody kicks.
/// </summary>
public class MatchShootoutTests
{
    private const int Seed = 7;

    /// <summary>
    /// Enough ticks for ninety minutes of football and the ticks the clock is held for,
    /// plus the shootout itself, which is a kick a tick and takes eleven of them at worst.
    /// </summary>
    private const int WholeMatchAndShootout = 900;

    private readonly Guid _homeId = Guid.NewGuid();
    private readonly Guid _awayId = Guid.NewGuid();

    private static List<MatchPlayerSnapshot> Lineup(Guid teamId, int offset, IRandomSource random)
    {
        var positions = new[]
        {
            Position.GK, Position.DEF, Position.DEF, Position.MID, Position.MID,
            Position.MID, Position.ATT, Position.ATT, Position.DEF, Position.MID, Position.MID
        };

        var lineup = new List<MatchPlayerSnapshot>();

        for (var index = 0; index < positions.Length; index++)
        {
            var player = Player.Create(
                $"Jogador {offset}-{index}",
                20 + index % 6,
                positions[index],
                speed: 12 + (index % 5),
                accuracy: 12 + (index % 5),
                dribbling: 12 + (index % 5),
                heading: 12 + (index % 5),
                strength: 12 + (index % 5),
                goalkeeperPower: 12 + (index % 5),
                reflexes: 12 + (index % 5));

            var state = PlayerSeasonState.Create(player.Id, Guid.NewGuid(), teamId, 88);
            lineup.Add(MatchPlayerSnapshot.FromPlayerSeasonState(player, state));
        }

        return lineup;
    }

    /// <summary>
    /// A match that has been set up and kicked off, with the engine that runs it and the
    /// one random source behind both — because a match is reproducible only if the
    /// kick-off and the ninety minutes come out of the same stream, and a test that plays
    /// a match twice to compare them has to be exactly that.
    /// </summary>
    private readonly record struct Prepared(MatchState State, MatchEngine Engine);

    private Prepared NewMatch(
        CupTieFacts? tie,
        Guid? managerTeamId = null,
        int seed = Seed)
    {
        var random = new DeterministicRandomSource(seed);
        var homeTeam = new TeamInfo(_homeId, "Casa United", "CU", "#FF0000", "#FFFFFF", 55);
        var awayTeam = new TeamInfo(_awayId, "Fora City", "FC", "#0000FF", "#FFFFFF", 50);

        var context = new MatchContext(
            Guid.NewGuid(),
            homeTeam,
            awayTeam,
            Lineup(_homeId, 0, random),
            Lineup(_awayId, 1, random),
            new List<MatchPlayerSnapshot>(),
            new List<MatchPlayerSnapshot>(),
            random,
            managerTeamId,
            tie);

        var state = new MatchState(context);
        var engine = new MatchEngine(random);
        engine.Initialize(state, 0).ToList();
        return new Prepared(state, engine);
    }

    /// <summary>
    /// The ninety minutes of a match with no tie to settle, played out to find out what the
    /// score is on this seed.
    ///
    /// The engine runs an action before it looks at the clock, so a test cannot simply put a
    /// score on the state and wind the clock forward: the last action of the match would
    /// change it. The match is played once to learn its own score and then played again with
    /// a tie around it, off the same seed, which draws the same actions and scores the same
    /// 1 x 0 at ninety minutes both times.
    /// </summary>
    private (int Home, int Away) PlayedNinetyMinutes(int seed = Seed)
    {
        var (state, engine) = NewMatch(null, null, seed);
        TickToTheEnd(state, engine);
        return (state.HomeScore, state.AwayScore);
    }

    /// <summary>
    /// Ticks until the match is over, playing the second half whenever the interval stops
    /// the clock — which it does, because nobody is watching and nothing would ever continue
    /// it.
    /// </summary>
    private static void TickToTheEnd(MatchState state, MatchEngine engine)
    {
        for (var tick = 0; tick < WholeMatchAndShootout && !state.MatchFinished; tick++)
        {
            engine.Tick(state).ToList();

            if (state.HalfTimePauseActive)
            {
                engine.ContinueSecondHalf(state);
            }
        }
    }

    /// <summary>
    /// Replays the same match with a tie around it, and returns the state at the moment the
    /// ninety minutes are over — which is either a finished match or one going to the spot.
    /// </summary>
    /// <param name="firstLegHomeGoals">The first leg's goals for the tie's home club.</param>
    /// <param name="firstLegAwayGoals">The first leg's goals for the tie's away club.</param>
    private MatchState ReplayedWithATie(
        int firstLegHomeGoals,
        int firstLegAwayGoals,
        Guid? managerTeamId = null,
        int seed = Seed)
    {
        var tie = CupTieFacts.ForSecondLeg(
            Guid.NewGuid(), _homeId, _awayId, firstLegHomeGoals, firstLegAwayGoals);

        var (state, engine) = NewMatch(tie, managerTeamId, seed);

        // The same ninety minutes, off the same seed, with a tie around them. The engine
        // scores the same goals at the same minutes and the clock stops in the same place,
        // so whatever the tie makes of that score is a fact about the tie and not about the
        // seed.
        for (var tick = 0; tick < WholeMatchAndShootout; tick++)
        {
            engine.Tick(state).ToList();

            if (state.HalfTimePauseActive)
            {
                engine.ContinueSecondHalf(state);
            }

            if (state.IsInShootout || state.MatchFinished)
            {
                return state;
            }
        }

        return state;
    }

    /// <summary>
    /// A first leg that makes the aggregate level after this seed's ninety minutes.
    ///
    /// The legs swap ends, so a level aggregate is
    /// <c>first leg home + this away == first leg away + this home</c> — which is only
    /// possible when the home side has not fallen behind in the ninety, because a first leg
    /// of a cup tie cannot end with a negative number of goals. The seed is therefore chosen
    /// for a ninety the home side did not lose, rather than the first leg being bent to fit
    /// a ninety that cannot be levelled.
    /// </summary>
    private (int FirstLegHome, int FirstLegAway) LevelFirstLegFor(int seed)
    {
        var (home, away) = PlayedNinetyMinutes(seed);
        var firstLegAway = Math.Max(0, away - home);

        return (firstLegAway + home - away, firstLegAway);
    }

    /// <summary>
    /// A seed whose ninety minutes the home side did not lose, so that a first leg exists
    /// which leaves the tie level after it.
    /// </summary>
    private int SeedOfALevellableTie()
    {
        for (var seed = Seed; seed < Seed + 50; seed++)
        {
            var (home, away) = PlayedNinetyMinutes(seed);

            if (home >= away)
            {
                return seed;
            }
        }

        throw new InvalidOperationException("No seed in fifty leaves a tie that can be levelled.");
    }

    /// <summary>The tie as it stands, level across, whatever the ninety minutes were.</summary>
    private MatchState ReplayedToALevelAggregate(Guid? managerTeamId = null)
    {
        var seed = SeedOfALevellableTie();
        var (firstLegHome, firstLegAway) = LevelFirstLegFor(seed);

        return ReplayedWithATie(firstLegHome, firstLegAway, managerTeamId, seed);
    }

    /// <summary>
    /// A tie whose aggregate the seed's ninety minutes leave level, for the tests that need
    /// the engine itself rather than a state they can hand back and keep ticking.
    /// </summary>
    private CupTieFacts LevelTie()
    {
        var seed = SeedOfALevellableTie();
        var (firstLegHome, firstLegAway) = LevelFirstLegFor(seed);

        return CupTieFacts.ForSecondLeg(
            Guid.NewGuid(), _homeId, _awayId, firstLegHome, firstLegAway);
    }

    /// <summary>The same match, on the same seed, with that tie around it.</summary>
    private Prepared MatchWithTheLevelTie(Guid? managerTeamId = null) =>
        NewMatch(LevelTie(), managerTeamId, SeedOfALevellableTie());

    [Fact]
    public void A_second_leg_level_on_the_aggregate_goes_to_the_spot_instead_of_ending()
    {
        // The ninety minutes are forced level, which is the only way to be sure the test is
        // about the aggregate rule rather than about whichever seed produced a draw.
        var state = ReplayedToALevelAggregate();

        // A tie that is level after two legs is decided at the spot. The match is not over,
        // and the ninety minutes are.
        Assert.NotNull(state.Shootout);
        Assert.False(state.MatchFinished);

        // The clock stops at the end of the ninety — plus whatever was added to it — and does
        // not move again for the shootout. A running clock under a shootout is a match that
        // is still being played.
        Assert.True(state.Minute >= MatchRules.MinutesInAMatch);
    }

    [Fact]
    public void A_second_leg_a_goal_clear_of_the_aggregate_finishes_at_ninety()
    {
        // The same ninety minutes, with a first leg that leaves the tie home side a goal
        // clear across. The tie is decided and there is nothing for a shootout to do.
        var (home, away) = PlayedNinetyMinutes();
        var state = ReplayedWithATie(firstLegHomeGoals: away + 1, firstLegAwayGoals: home);

        Assert.True(state.MatchFinished);
        Assert.Null(state.Shootout);
    }

    [Fact]
    public void The_aggregate_is_counted_by_club_and_not_by_side()
    {
        // The ninety minutes counted as the tie's *away* side: a goal more of theirs in the
        // second leg is a goal more of the tie home side's aggregate. Reading it as this
        // match's home and away would send a level ninety minutes to the spot.
        var (home, away) = PlayedNinetyMinutes();
        var state = ReplayedWithATie(firstLegHomeGoals: away, firstLegAwayGoals: home + 1);

        Assert.True(state.MatchFinished);
        Assert.Null(state.Shootout);
    }

    [Fact]
    public void A_championship_match_level_at_ninety_is_finished_at_ninety()
    {
        // Ninety minutes of a league game that ends level is a draw, and a draw is a result.
        // Only a tie that is still undecided goes to penalties, and a league game is not one.
        var (state, engine) = NewMatch(tie: null, managerTeamId: null, Seed);
        TickToTheEnd(state, engine);

        Assert.True(state.MatchFinished);
        Assert.Null(state.Shootout);
    }

    [Fact]
    public void A_manager_names_the_order_of_his_own_five_before_anybody_kicks()
    {
        var (state, engine) = MatchWithTheLevelTie(_homeId);
        TickToTheEnd(state, engine);

        Assert.True(state.ShootoutAwaitingOrder);
        Assert.NotNull(state.Shootout);

        // The clock stands still while he decides. A shootout whose first five men are
        // chosen by the engine is not one the manager entered.
        var waiting = engine.Tick(state).ToList();
        Assert.Empty(waiting);
        Assert.Equal(0, state.Shootout!.Kicks.Count);

        // Any order he likes out of the pool he was offered, and the kicks are taken in it.
        var chosen = state.HomeShootoutTakers.Take(5).Reverse().ToList();
        engine.NameShootoutOrder(state, _homeId, chosen);

        Assert.Equal(chosen, state.Shootout.HomeTakers);
        Assert.False(state.ShootoutAwaitingOrder);

        // The first kick is whoever's the coin sent, and the manager's men are taken in the
        // order he named them — the substitute he put first is the first of his to go. Two
        // ticks, because the coin may have sent the other club to the spot.
        engine.Tick(state).ToList();
        engine.Tick(state).ToList();

        Assert.Equal(2, state.Shootout.Kicks.Count);
        var firstHomeKick = state.Shootout.Kicks.First(kick => kick.TeamId == _homeId);
        Assert.Equal(chosen[0], firstHomeKick.TakerId);
    }

    [Fact]
    public void A_manager_may_not_name_a_man_who_is_not_in_the_pool()
    {
        var (state, engine) = MatchWithTheLevelTie(_homeId);
        TickToTheEnd(state, engine);

        var stranger = Guid.NewGuid();

        // He may not name a man who never played, and he may not name more men than the
        // pool holds. Both refusals are the same refusal: the pool is what the Laws leave
        // him to choose from, and it is not larger than it is.
        Assert.Throws<ArgumentException>(() =>
            engine.NameShootoutOrder(state, _homeId, state.HomeShootoutTakers.Append(stranger).ToList()));
        Assert.Throws<ArgumentException>(() =>
            engine.NameShootoutOrder(state, _homeId, Array.Empty<Guid>()));
        Assert.Throws<InvalidOperationException>(() =>
            engine.NameShootoutOrder(state, _awayId, state.AwayShootoutTakers.Take(5).ToList()));

        Assert.Equal(0, state.Shootout!.HomeTakers.Count);
        Assert.True(state.ShootoutAwaitingOrder);
    }

    [Fact]
    public void The_other_club_s_order_is_the_engines_own_and_needs_nobody_to_ask_for_it()
    {
        var state = ReplayedToALevelAggregate(managerTeamId: _homeId);

        // The away order was filled the moment the shootout opened: nobody is watching it
        // and a shootout that waited for a manager nobody has is a shootout that never runs.
        // The one order still missing is the manager's own, and it is missing because he has
        // not made it yet.
        Assert.Equal(5, state.Shootout.AwayTakers.Count);
        Assert.Empty(state.Shootout.HomeTakers);
        Assert.False(state.Shootout.BothOrdersNamed);
        Assert.True(state.ShootoutAwaitingOrder);
    }

    [Fact]
    public void A_match_nobody_is_watching_goes_straight_through_the_shootout_to_the_end()
    {
        var (state, engine) = MatchWithTheLevelTie();
        var types = new List<MatchEventType>();

        // One tick at a time, all the way through: the ninety minutes, the shootout and the
        // last kick, which is the point — the shootout is part of the match and it is
        // watched kick by kick rather than resolved in one go behind the result.
        for (var tick = 0; tick < WholeMatchAndShootout && !state.MatchFinished; tick++)
        {
            types.AddRange(engine.Tick(state).Select(e => e.Type));

            if (state.HalfTimePauseActive)
            {
                engine.ContinueSecondHalf(state);
            }
        }

        // Every kick is its own beat, and the match ends on the last one: the shootout is
        // part of the match, not a number written on it afterwards.
        Assert.Single(types.Where(type => type == MatchEventType.PenaltyShootoutStarted));
        Assert.True(types.Count(type => type == MatchEventType.PenaltyShootoutKick) >= 6);
        Assert.True(state.MatchFinished);
        Assert.True(state.Shootout!.IsComplete);
        Assert.NotEqual(_homeId, Guid.Empty);
        Assert.Contains(types, type => type == MatchEventType.MatchFinished);
    }

    /// <summary>
    /// Every kick goes into the feed, not only into the list of things the tick returned.
    ///
    /// The two are not the same: the feed is what the service drains, what the database is
    /// given and what a manager watching is sent, and a shootout that filled the first and
    /// not the second would be a tie that opened at the spot, was decided at the spot, and
    /// left no record of a single man who walked up to take one. That is what happened, and
    /// this is the test that says it may not happen again.
    /// </summary>
    [Fact]
    public void Every_kick_of_the_shootout_is_in_the_feed_and_the_feed_is_not_read_twice()
    {
        var (state, engine) = MatchWithTheLevelTie();

        for (var tick = 0; tick < WholeMatchAndShootout && !state.MatchFinished; tick++)
        {
            engine.Tick(state);

            if (state.HalfTimePauseActive)
            {
                engine.ContinueSecondHalf(state);
            }
        }

        var feed = state.PendingFeed.Select(e => e.Type).ToList();

        Assert.Contains(MatchEventType.PenaltyShootoutStarted, feed);
        Assert.Equal(
            state.Shootout!.Kicks.Count,
            feed.Count(type => type == MatchEventType.PenaltyShootoutKick));
        Assert.Contains(MatchEventType.MatchFinished, feed);
    }

    [Fact]
    public void The_shootout_takers_are_the_men_who_played_and_not_the_whole_squad()
    {
        // The rule itself, on a lineup drawn by hand: a man sent off and a man who never
        // came on are both out, and the keeper and the substitutes who played are in. The
        // Laws name both exclusions, and this is the one place they are written down.
        var lineup = Lineup(_homeId, 0, new DeterministicRandomSource(7));
        foreach (var player in lineup)
        {
            player.PlayedInMatch = true;
        }

        var sentOff = lineup.First(player => player.Position == Position.ATT);
        sentOff.SendOff(35);

        var reserve = MatchPlayerSnapshot.FromPlayerSeasonState(
            Player.Create("Nunca Jogou", 24,
                Position.ATT, 15, 15, 15, 15, 15, 15, 15),
            PlayerSeasonState.Create(Guid.NewGuid(), Guid.NewGuid(), _homeId, 99));

        var eligible = ShootoutRules.EligibleTakers(lineup.Append(reserve).ToList());

        Assert.DoesNotContain(eligible, player => player.PlayerId == sentOff.PlayerId);
        Assert.DoesNotContain(eligible, player => player.PlayerId == reserve.PlayerId);
        Assert.Contains(eligible, player => player.Position == Position.GK);
        Assert.Equal(10, eligible.Count);

        // And the pool a match actually offers is drawn from the men who played, never more
        // than the men on the pitch.
        var state = ReplayedToALevelAggregate();
        Assert.NotNull(state.Shootout);
        Assert.NotEmpty(state.HomeShootoutTakers);
        Assert.All(state.HomeShootoutTakers, id =>
            Assert.True(state.HomeLineup.Any(player => player.PlayerId == id)));
        Assert.True(state.HomeShootoutTakers.Count <= state.HomeLineup.Count);
        Assert.All(state.HomeBench.Where(player => !player.PlayedInMatch),
            player => Assert.DoesNotContain(player.PlayerId, state.HomeShootoutTakers));
    }
}
