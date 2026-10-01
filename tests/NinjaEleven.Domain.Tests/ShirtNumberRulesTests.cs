using NinjaEleven.Domain.Teams;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// What a player wears, and who may have it.
///
/// The assertions are about the two things a manager will notice if either is wrong: a club
/// where two men are on the same shirt, and a keeper wearing a striker's number. Everything
/// else here is the arithmetic that keeps those two from happening.
/// </summary>
public class ShirtNumberRulesTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100)]
    [InlineData(1000)]
    public void A_number_outside_one_to_ninety_nine_is_not_a_number_a_player_wears(int number) =>
        Assert.False(ShirtNumberRules.IsValid(number));

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(99)]
    public void A_number_inside_the_range_is_one(int number) =>
        Assert.True(ShirtNumberRules.IsValid(number));

    [Fact]
    public void A_squad_is_numbered_one_for_the_first_goalkeeper_twelve_for_the_second_and_twenty_three_for_the_third()
    {
        var squad = new[] { true, true, true, false, false, false };

        var dealt = ShirtNumberRules.Deal(squad);

        Assert.Equal(new[] { 1, 12, 23, 2, 3, 4 }, dealt);
    }

    /// <summary>
    /// The three keeper numbers are reserved rather than merely dealt, so a club whose outfield
    /// players have taken the two and the three still has a goalkeeper.
    /// </summary>
    [Fact]
    public void A_goalkeeper_does_not_arrive_in_the_number_a_striker_is_wearing()
    {
        var squad = new[] { false, false, false, false, true };

        var dealt = ShirtNumberRules.Deal(squad);

        Assert.Equal(new[] { 2, 3, 4, 5, 1 }, dealt);
        Assert.DoesNotContain(2, dealt.Skip(4));
    }

    [Fact]
    public void An_outfield_player_is_never_given_the_number_one_even_when_it_is_free()
    {
        var dealt = ShirtNumberRules.Deal([false, false]);

        Assert.Equal(new[] { 2, 3 }, dealt);
    }

    [Fact]
    public void A_squad_is_given_each_number_once()
    {
        var dealt = ShirtNumberRules.Deal([true, true, true, false, false, false, false, false, false, false]);

        Assert.Equal(dealt.Count, dealt.Distinct().Count());
    }

    /// <summary>
    /// Twenty-three men is a real squad, so twenty-three men is a real deal. The assertion is
    /// that it is twenty-three distinct shirts rather than that they are 1..23, because the
    /// keepers' numbers push the outfield numbering along.
    /// </summary>
    [Fact]
    public void A_full_squad_of_twenty_three_is_dealt_twenty_three_different_shirts()
    {
        var dealt = ShirtNumberRules.Deal([true, true, true, .. Enumerable.Repeat(false, 20)]);

        Assert.Equal(23, dealt.Count);
        Assert.Equal(23, dealt.Distinct().Count());
        Assert.Contains(1, dealt);
        Assert.Contains(12, dealt);
        Assert.Contains(23, dealt);
    }

    [Fact]
    public void A_signing_takes_the_lowest_number_nobody_is_wearing()
    {
        Assert.Equal(4, ShirtNumberRules.LowestFree([1, 2, 3]));
    }

    /// <summary>
    /// A keeper who leaves takes his shirt with him, so the number waits for the next keeper
    /// rather than going to whoever signs next.
    /// </summary>
    [Fact]
    public void A_gone_goalkeepers_number_waits_for_a_goalkeeper()
    {
        // Two, three and four are on three outfield players; one is nobody's.
        Assert.Equal(1, ShirtNumberRules.ForGoalkeeper([12, 23, 2, 3, 4]));
        Assert.Equal(5, ShirtNumberRules.For([12, 23, 2, 3, 4], isGoalkeeper: false));
    }

    [Fact]
    public void A_fourth_goalkeeper_takes_the_lowest_number_left_rather_than_a_reserved_one()
    {
        Assert.Equal(2, ShirtNumberRules.ForGoalkeeper([1, 12, 23, 3, 4]));
    }

    [Fact]
    public void The_word_is_about_a_club() =>
        Assert.Equal("ShirtNumberAlreadyTaken", ShirtNumberRules.AlreadyTakenCode);

    [Fact]
    public void The_word_for_a_number_that_does_not_exist_is_not_the_word_for_one_that_is_taken() =>
        Assert.NotEqual(ShirtNumberRules.OutOfRangeCode, ShirtNumberRules.AlreadyTakenCode);
}

/// <summary>
/// The shirt is the contract's, so it is set on the contract and it is refused by the contract.
/// </summary>
public class TeamMembershipShirtTests
{
    private static TeamMembership Contract(DateOnly? start = null) =>
        TeamMembership.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            start ?? new DateOnly(2026, 1, 1),
            shirtNumber: 7);

    [Fact]
    public void A_contract_signed_with_a_number_carries_it()
    {
        var contract = Contract();

        Assert.Equal(7, contract.ShirtNumber);
        Assert.True(contract.HasShirtNumber);
    }

    [Fact]
    public void A_contract_signed_without_one_has_not_been_dressed_yet()
    {
        var contract = TeamMembership.Create(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 1, 1));

        Assert.Null(contract.ShirtNumber);
        Assert.False(contract.HasShirtNumber);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void A_number_outside_the_range_is_refused_by_the_contract_itself(int number)
    {
        var contract = Contract();

        Assert.Throws<ArgumentOutOfRangeException>(() => contract.WearNumber(number));
    }

    [Fact]
    public void A_manager_may_put_a_man_in_a_different_shirt()
    {
        var contract = Contract();

        contract.WearNumber(30);

        Assert.Equal(30, contract.ShirtNumber);
    }

    [Fact]
    public void A_contract_signed_with_a_number_outside_the_range_never_gets_that_far()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TeamMembership.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 1, 1),
            shirtNumber: 100));
    }
}
