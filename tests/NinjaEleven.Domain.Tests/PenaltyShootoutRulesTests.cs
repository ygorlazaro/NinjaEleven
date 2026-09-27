using NinjaEleven.Domain.Matches;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// The rules of a penalty shootout, as the Laws state them.
///
/// Each of these is a rule that used to live inside a loop that drew both sides' kicks at
/// once and wrote the answer to the tie, so a manager never saw one and a Laws breach could
/// not be told apart from bad luck. They are written here as separate facts because each one
/// has its own failure: a shootout that alternates wrongly is not a shootout, a shootout
/// that plays on after a side is mathematically out is theatre, and a shootout that ends
/// level is a tie with no winner in it.
/// </summary>
public class PenaltyShootoutRulesTests
{
    private static readonly Guid Home = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Away = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>Five men a side, named in the order a manager would name them.</summary>
    private static Guid[] Men(Guid club, int count) =>
        Enumerable.Range(0, count).Select(index => Guid.NewGuid()).ToArray();

    private static Shootout NewShootout(bool homeFirst = true, int men = 5) =>
        Shootout.Begin(Home, Away, homeFirst, Men(Home, men), Men(Away, men));

    [Fact]
    public void The_toss_decides_who_kicks_first_and_nothing_else()
    {
        var homeFirst = NewShootout(homeFirst: true);
        var awayFirst = NewShootout(homeFirst: false);

        Assert.True(homeFirst.NextTeamIsHome);
        Assert.False(awayFirst.NextTeamIsHome);
    }

    [Fact]
    public void The_kicks_alternate_from_the_first()
    {
        var shootout = NewShootout();
        var takers = new List<Guid>();

        for (var kick = 0; kick < 4; kick++)
        {
            var home = shootout.NextTeamIsHome!.Value;
            takers.Add(shootout.NextTaker(home)!.Value);
            shootout.Take(takers[^1], scored: true);
        }

        // The first kick is the toss winner's, and then the other side, and then the first
        // again: a manager who cannot tell from the feed whose turn it is is watching a
        // shootout that does not look like one.
        Assert.Equal(
            new[] { shootout.HomeTakers[0], shootout.AwayTakers[0], shootout.HomeTakers[1], shootout.AwayTakers[1] },
            takers);
    }

    [Fact]
    public void A_side_two_ahead_with_two_kicks_left_has_already_won_it()
    {
        // Home scores its first three; away misses its first two and it is 3-0 with three
        // kicks taken, so two up with one kick each left to come. The shootout is over.
        var shootout = NewShootout();

        shootout.Take(shootout.NextTaker(true)!.Value, scored: true);
        shootout.Take(shootout.NextTaker(false)!.Value, scored: false);
        shootout.Take(shootout.NextTaker(true)!.Value, scored: true);
        shootout.Take(shootout.NextTaker(false)!.Value, scored: false);
        shootout.Take(shootout.NextTaker(true)!.Value, scored: true);
        shootout.Take(shootout.NextTaker(false)!.Value, scored: false);

        Assert.True(shootout.IsComplete);
        Assert.Equal(Home, shootout.WinnerTeamId);
        Assert.Equal(3, shootout.HomeGoals);
        Assert.Equal(0, shootout.AwayGoals);
    }

    [Fact]
    public void A_lead_the_other_side_can_still_catch_does_not_end_it()
    {
        // 1-0 after the first pair, with four kicks each still to take: the shootout goes on.
        var shootout = NewShootout();

        shootout.Take(shootout.NextTaker(true)!.Value, scored: true);
        shootout.Take(shootout.NextTaker(false)!.Value, scored: false);

        Assert.False(shootout.IsComplete);
        Assert.Equal(1, shootout.HomeGoals);
        Assert.Equal(0, shootout.AwayGoals);
        Assert.Equal(1, shootout.HomeKicksTaken);
        Assert.Equal(1, shootout.AwayKicksTaken);
    }

    [Fact]
    public void Five_each_all_scored_and_the_shootout_goes_to_sudden_death()
    {
        var shootout = NewShootout();

        for (var kick = 0; kick < 10; kick++)
        {
            Assert.False(shootout.IsComplete, "5-0 after five each is not a result");
            shootout.Take(shootout.NextTaker(shootout.NextTeamIsHome!.Value)!.Value, scored: true);
        }

        // The end of the fifth pair is not yet sudden death: nothing is decided, and the
        // eleventh kick is the first one that has no finish line in front of it.
        Assert.False(shootout.IsSuddenDeath);
        Assert.False(shootout.IsComplete);
        Assert.Equal(5, shootout.HomeGoals);
        Assert.Equal(5, shootout.AwayGoals);

        shootout.Take(shootout.NextTaker(shootout.NextTeamIsHome!.Value)!.Value, scored: true);

        Assert.True(shootout.IsSuddenDeath);
    }

    [Fact]
    public void Sudden_death_ends_on_the_first_difference_after_the_pair()
    {
        var shootout = NewShootout();

        for (var kick = 0; kick < 10; kick++)
        {
            shootout.Take(shootout.NextTaker(shootout.NextTeamIsHome!.Value)!.Value, scored: true);
        }

        // A level pair is two more kicks, and a scored one is the answer.
        shootout.Take(shootout.NextTaker(shootout.NextTeamIsHome!.Value)!.Value, scored: true);
        Assert.False(shootout.IsComplete);

        shootout.Take(shootout.NextTaker(shootout.NextTeamIsHome!.Value)!.Value, scored: false);

        Assert.True(shootout.IsComplete);
        Assert.Equal(Home, shootout.WinnerTeamId);
    }

    [Fact]
    public void The_first_difference_in_sudden_death_can_be_the_away_side()
    {
        var shootout = NewShootout(homeFirst: false);

        for (var kick = 0; kick < 10; kick++)
        {
            shootout.Take(shootout.NextTaker(shootout.NextTeamIsHome!.Value)!.Value, scored: true);
        }

        // Away goes first, so it is the one that has to score for the difference to be a
        // result: a level pair is two more kicks and not a draw.
        shootout.Take(shootout.NextTaker(shootout.NextTeamIsHome!.Value)!.Value, scored: true);
        shootout.Take(shootout.NextTaker(shootout.NextTeamIsHome!.Value)!.Value, scored: false);

        Assert.True(shootout.IsComplete);
        Assert.Equal(Away, shootout.WinnerTeamId);
    }

    [Fact]
    public void A_shootout_never_ends_level()
    {
        // Every kick of the first ten scored, then the pair is level too. A shootout that
        // can finish 6-6 is a tie with no winner, and the Laws do not have one.
        var shootout = NewShootout();

        for (var kick = 0; kick < 12; kick++)
        {
            shootout.Take(shootout.NextTaker(shootout.NextTeamIsHome!.Value)!.Value, scored: true);
        }

        Assert.False(shootout.IsComplete);
        Assert.Equal(Guid.Empty, shootout.WinnerTeamId);
    }

    [Fact]
    public void Every_kick_is_taken_by_a_different_man()
    {
        var shootout = NewShootout();
        var takers = new List<Guid>();

        for (var kick = 0; kick < 10; kick++)
        {
            var taker = shootout.NextTaker(shootout.NextTeamIsHome!.Value)!.Value;
            takers.Add(taker);
            shootout.Take(taker, scored: true);
        }

        // Five kicks a side, five different men each, in the order the manager named them.
        Assert.Equal(shootout.HomeTakers, takers.Where((_, index) => index % 2 == 0));
        Assert.Equal(shootout.AwayTakers, takers.Where((_, index) => index % 2 == 1));
        Assert.Equal(10, takers.Distinct().Count());
    }

    [Fact]
    public void A_side_with_fewer_men_than_kicks_comes_back_to_the_men_it_used()
    {
        // The Laws: a man who has kicked may kick again once everybody else has. A club
        // that can only send four men cannot take five, and the fifth is the first of them
        // again rather than nobody.
        var shootout = Shootout.Begin(
            Home, Away, true, Men(Home, 4), Men(Away, 5));

        var taken = new List<Guid>();
        for (var kick = 0; kick < 10; kick++)
        {
            var taker = shootout.NextTaker(shootout.NextTeamIsHome!.Value)!.Value;
            taken.Add(taker);
            shootout.Take(taker, scored: true);
        }

        // The kicks go home, away, home, away — so the home side's fifth kick is the ninth
        // taken, and it is the first man again because he is the only one left.
        var homeTakers = taken.Where((_, index) => index % 2 == 0).ToList();
        Assert.Equal(5, homeTakers.Count);
        Assert.Equal(shootout.HomeTakers, homeTakers.Take(4));
        Assert.Equal(shootout.HomeTakers[0], homeTakers[4]);
    }

    [Fact]
    public void A_shootout_needs_two_clubs_and_two_teams_that_can_kick()
    {
        Assert.Throws<ArgumentException>(() =>
            Shootout.Begin(Home, Home, true, Men(Home, 5), Men(Away, 5)));

        Assert.Throws<ArgumentException>(() =>
            Shootout.Begin(Home, Away, true, Array.Empty<Guid>(), Men(Away, 5)));
    }

    [Fact]
    public void A_decided_shootout_takes_no_more_kicks()
    {
        var shootout = NewShootout();

        shootout.Take(shootout.NextTaker(true)!.Value, scored: true);
        shootout.Take(shootout.NextTaker(false)!.Value, scored: false);
        shootout.Take(shootout.NextTaker(true)!.Value, scored: true);
        shootout.Take(shootout.NextTaker(false)!.Value, scored: false);
        shootout.Take(shootout.NextTaker(true)!.Value, scored: true);
        shootout.Take(shootout.NextTaker(false)!.Value, scored: false);

        Assert.Throws<InvalidOperationException>(() => shootout.Take(Guid.NewGuid(), true));
    }
}
