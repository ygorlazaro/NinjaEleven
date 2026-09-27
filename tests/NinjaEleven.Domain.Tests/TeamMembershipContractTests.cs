using NinjaEleven.Domain.Teams;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// A contract's clock, which is what a transfer is negotiated against.
///
/// The rule is one subtraction — the season being played, less the season he was signed in,
/// taken off the seasons he was promised — and everything that matters is in its edges: a
/// contract signed for the season in progress has all of it left, the last season of a
/// three-year deal is the one a rival can sign him in without buying years, and a contract
/// that has run out is not negative.
/// </summary>
public class TeamMembershipContractTests
{
    private static readonly Guid PlayerId = Guid.NewGuid();
    private static readonly Guid TeamId = Guid.NewGuid();

    private static TeamMembership SignedIn(int season, int seasons = 3) =>
        TeamMembership.Create(
            PlayerId,
            TeamId,
            new DateOnly(2020 + season, 1, 1),
            contractSeasons: seasons,
            startSeasonNumber: season);

    [Fact]
    public void A_contract_signed_this_season_has_all_of_it_left()
    {
        Assert.Equal(3, SignedIn(1).SeasonsLeft(1));
    }

    [Fact]
    public void A_season_burns_one_of_the_years_of_a_contract()
    {
        var membership = SignedIn(1);

        Assert.Equal(3, membership.SeasonsLeft(1));
        Assert.Equal(2, membership.SeasonsLeft(2));
        Assert.Equal(1, membership.SeasonsLeft(3));
    }

    [Fact]
    public void A_contract_that_has_run_out_has_nothing_left_and_not_a_negative()
    {
        var membership = SignedIn(1);

        Assert.Equal(0, membership.SeasonsLeft(4));
        Assert.Equal(0, membership.SeasonsLeft(9));
    }

    [Fact]
    public void The_last_season_of_a_contract_is_the_one_a_rival_can_sign_him_in()
    {
        var membership = SignedIn(1);

        Assert.False(membership.IsInHisLastSeason(1));
        Assert.False(membership.IsInHisLastSeason(2));
        Assert.True(membership.IsInHisLastSeason(3));
        Assert.True(membership.IsInHisLastSeason(4));
    }

    [Fact]
    public void A_one_season_contract_is_its_own_last_season_from_the_start()
    {
        var membership = SignedIn(1, seasons: 1);

        Assert.Equal(1, membership.SeasonsLeft(1));
        Assert.True(membership.IsInHisLastSeason(1));
    }

    [Fact]
    public void A_longer_contract_keeps_a_man_unbuyable_for_longer()
    {
        var threeSeasons = SignedIn(1, seasons: 3);
        var fiveSeasons = SignedIn(1, seasons: 5);

        Assert.Equal(2, threeSeasons.SeasonsLeft(2));
        Assert.Equal(4, fiveSeasons.SeasonsLeft(2));
    }

    [Fact]
    public void A_contract_signed_several_seasons_ago_is_already_running()
    {
        // Signed in the 2024 season for three: by the time the world's 2026 season is being
        // played, two of them are spent and the man is free at the end of this one.
        var membership = SignedIn(2024);

        Assert.Equal(3, membership.SeasonsLeft(2024));
        Assert.Equal(2, membership.SeasonsLeft(2025));
        Assert.Equal(1, membership.SeasonsLeft(2026));
        Assert.Equal(0, membership.SeasonsLeft(2027));
    }

    [Fact]
    public void A_calendar_that_keeps_an_extra_day_does_not_make_a_contract_shorter()
    {
        // The clock is counted in seasons and not in days, so a leap year cannot turn a
        // three-season deal into a two-season one by being one day longer than its
        // neighbours. The membership does not even know what day it was signed on.
        var membership = TeamMembership.Create(
            PlayerId,
            TeamId,
            new DateOnly(2024, 2, 29),
            contractSeasons: 3,
            startSeasonNumber: 1);

        Assert.Equal(3, membership.SeasonsLeft(1));
        Assert.Equal(2, membership.SeasonsLeft(2));
        Assert.Equal(1, membership.SeasonsLeft(3));
        Assert.Equal(0, membership.SeasonsLeft(4));
    }

    [Fact]
    public void A_contract_is_signed_in_a_season_of_the_world()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TeamMembership.Create(
            PlayerId,
            TeamId,
            new DateOnly(2026, 1, 1),
            contractSeasons: 3,
            startSeasonNumber: 0));
    }

    [Fact]
    public void A_contract_signed_halfway_through_the_season_still_owes_every_season_of_it()
    {
        // The day of the month is a fact about the paperwork and not about the promise: a
        // club that paid for three seasons got three seasons, whoever signed it in July.
        var membership = TeamMembership.Create(
            PlayerId,
            TeamId,
            new DateOnly(2026, 7, 1),
            contractSeasons: 3,
            startSeasonNumber: 1);

        Assert.Equal(3, membership.SeasonsLeft(1));
        Assert.Equal(2, membership.SeasonsLeft(2));
    }
}
