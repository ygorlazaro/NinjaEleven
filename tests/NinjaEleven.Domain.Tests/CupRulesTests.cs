using NinjaEleven.Domain.Competitions;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// The cup's rules, read as data.
///
/// <c>CupRules</c> is what a "prêmios" screen prints beside the money, and the whole reason it
/// exists is that the bracket cannot say any of it: the bracket says which two clubs are in a tie
/// and the prize legend says what the run is worth, and nothing in between says that the tie is
/// two matches, that the aggregate decides it, or that a level aggregate goes to penalties.
///
/// What these tests hold is that the page says the competition the game plays. A "dois jogos" of
/// its own on a client is a promise the calendar does not keep, and the failure is invisible
/// until the day the constants move — at which point the bracket it was explaining is still the
/// bracket the game drew, and the two are describing different competitions.
/// </summary>
public class CupRulesTests
{
    [Fact]
    public void The_rules_are_the_constants_the_cup_is_drawn_from()
    {
        Assert.Equal(CompetitionRules.CupSize, CupRules.Size);
        Assert.Equal(CompetitionRules.CupRounds, CupRules.TieRounds);
        Assert.Equal(CompetitionRules.CupTieLegs, CupRules.LegsPerTie);
        Assert.Equal(CompetitionRules.CupRounds, CupRules.Rounds().Count);
    }

    [Fact]
    public void There_are_as_many_tie_rounds_as_the_cup_halves_for()
    {
        // Sixty-four is a power of two, which is the only reason the last round is a final
        // between two clubs rather than a round that leaves one club with nobody to play. A cup
        // drawn to sixty-two would have to stop somewhere with a bye, and a rules page that
        // listed six rounds regardless would be describing a competition the drawer never held.
        var clubs = CompetitionRules.CupSize;

        foreach (var round in CupRules.Rounds())
        {
            Assert.Equal(clubs, round.ClubsIn);
            Assert.Equal(clubs / 2, round.Ties);
            Assert.Equal(0, clubs % 2);
            clubs /= 2;
        }

        // One club is left out of the field, and it is the champion: sixty-four goes into the
        // first round and one comes out of the last, so a cup that left two would be handing a
        // second club a place nobody is there to take.
        Assert.Equal(1, clubs);
    }

    [Fact]
    public void Each_round_halves_into_the_next_and_the_final_is_two_clubs()
    {
        var rounds = CupRules.Rounds();

        Assert.Equal(64, rounds[0].ClubsIn);
        Assert.Equal(2, rounds[^1].ClubsIn);
        Assert.Equal(1, rounds[^1].Ties);

        for (var index = 1; index < rounds.Count; index++)
        {
            Assert.Equal(rounds[index - 1].ClubsIn / 2, rounds[index].ClubsIn);
        }
    }

    [Fact]
    public void A_round_is_named_and_played_over_the_two_days_the_calendar_gives_it()
    {
        // The names and the days are the same ones the drawer and the calendar use, so a manager
        // reading the rules sees the round the bracket is about to show rather than a second
        // naming of it.
        foreach (var round in CupRules.Rounds())
        {
            var (first, second) = CompetitionRules.CupLegMatchDays(round.TieRound);

            Assert.Equal(CompetitionRules.TieRoundName(round.TieRound), round.Name);
            Assert.Equal(first, round.FirstLegDay);
            Assert.Equal(second, round.SecondLegDay);
        }
    }

    [Fact]
    public void The_rules_say_a_tie_is_two_legs_and_the_aggregate_decides_it()
    {
        // A manager who loses the return by one and won the first leg by two is still in the cup,
        // and the only way he knows that before the match rather than after it is a rules page
        // that says the aggregate is what is added up.
        Assert.Equal(2, CupRules.LegsPerTie);
        Assert.Contains("agregado", CupRules.AggregateRule, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2", CupRules.AggregateRule);
    }

    [Fact]
    public void A_tie_level_on_the_aggregate_is_refused_rather_than_decided()
    {
        // The rule and the engine are one claim, so the test asks the engine: a level aggregate
        // with no shootout has to throw rather than pick a winner. A rules page saying "vai para os
        // pênaltis" is only true because this is where a tie with nothing to separate them stops.
        var tie = CupTie.Create(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => tie.Resolve(2, 2));
    }

    [Fact]
    public void The_rules_say_a_level_tie_goes_to_penalties_and_not_to_extra_time()
    {
        // Extra time is sent as a flag rather than left out, because its absence is the rule: a
        // manager planning a tie he expects to be level needs to know the game will not give
        // anybody thirty more minutes.
        Assert.False(CupRules.AllowsExtraTime);
        Assert.Contains("pênaltis", CupRules.LevelTieRule, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_rules_say_the_winner_goes_on_to_the_supercup()
    {
        // The one thing a knockout decides about the season after it. The cup runs across the
        // pyramid, so this is the only place a third-division club reaches the Supercup — and a
        // page that left it out would make a season's two titles look like one.
        Assert.Contains("Supercopa", CupRules.WinnerTakesRule, StringComparison.OrdinalIgnoreCase);
    }
}
