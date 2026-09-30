using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// A matchday is a date and the hours its windows go out, and a date on its own is not a
/// moment. This is the arithmetic every "is it due yet" question in the world is built on, so
/// it is pinned here rather than left to the first person who changes a kick-off time.
/// </summary>
public class MatchDayScheduleTests
{
    [Fact]
    public void A_window_goes_out_at_the_hour_its_competition_goes_out_at()
    {
        // The championship opens the afternoon at fifteen hundred and the cup closes the evening
        // at twenty-one hundred, and the Supercup goes out with the championship because it is
        // alone on its day. Six hours, and the world can therefore say which of them is due
        // without anybody remembering.
        var matchDay = MatchDay.Create(Guid.NewGuid(), 1, new DateOnly(2026, 3, 7));

        Assert.Equal(new DateTimeOffset(2026, 3, 7, 15, 0, 0, TimeSpan.Zero), matchDay.KickOffAt(CompetitionType.SuperCup));
        Assert.Equal(new DateTimeOffset(2026, 3, 7, 15, 0, 0, TimeSpan.Zero), matchDay.KickOffAt(CompetitionType.League));
        Assert.Equal(new DateTimeOffset(2026, 3, 7, 21, 0, 0, TimeSpan.Zero), matchDay.KickOffAt(CompetitionType.Cup));
    }

    [Fact]
    public void A_window_is_not_due_while_the_world_is_still_outside_its_tolerance()
    {
        // Half an hour either side of the hour is inside the tolerance and a minute beyond it
        // is not, so a process that woke up at eight does not start the evening's cup at eight.
        var matchDay = MatchDay.Create(Guid.NewGuid(), 1, new DateOnly(2026, 3, 7));
        var tolerance = TimeSpan.FromMinutes(30);

        Assert.False(matchDay.IsDue(
            matchDay.KickOffAt(CompetitionType.Cup),
            new DateTimeOffset(2026, 3, 7, 20, 29, 0, TimeSpan.Zero),
            tolerance));
    }

    [Fact]
    public void A_window_is_due_the_moment_the_world_is_inside_its_tolerance()
    {
        var matchDay = MatchDay.Create(Guid.NewGuid(), 1, new DateOnly(2026, 3, 7));
        var tolerance = TimeSpan.FromMinutes(30);

        Assert.True(matchDay.IsDue(
            matchDay.KickOffAt(CompetitionType.Cup),
            new DateTimeOffset(2026, 3, 7, 21, 45, 0, TimeSpan.Zero),
            tolerance));
    }

    [Fact]
    public void A_window_is_due_the_moment_its_hour_arrives()
    {
        var matchDay = MatchDay.Create(Guid.NewGuid(), 1, new DateOnly(2026, 3, 7));
        var tolerance = TimeSpan.FromMinutes(30);

        Assert.True(matchDay.IsDue(
            matchDay.KickOffAt(CompetitionType.League),
            new DateTimeOffset(2026, 3, 7, 15, 0, 0, TimeSpan.Zero),
            tolerance));
    }

    [Fact]
    public void A_window_that_was_missed_is_still_due_the_next_day()
    {
        // A world that was down over a matchday owes it. Treating the missed day as gone is
        // how a season ends up permanently a round behind its own results, and a tolerance
        // is not a deadline: it is how far off the hour a window may be run, not how long it
        // stays claimable.
        var matchDay = MatchDay.Create(Guid.NewGuid(), 1, new DateOnly(2026, 3, 7));
        var tolerance = TimeSpan.FromMinutes(30);

        Assert.True(matchDay.IsDue(
            matchDay.KickOffAt(CompetitionType.League),
            new DateTimeOffset(2026, 3, 9, 9, 0, 0, TimeSpan.Zero),
            tolerance));
    }

    [Fact]
    public void A_days_two_windows_are_never_due_at_the_same_moment()
    {
        // The guard on the bug this calendar had: a day that holds a cup leg holds it six hours
        // after its round, and when the round goes out the leg is not due yet. A world that
        // could not tell a window of today from a window of yesterday would play the day's whole
        // backlog in one pass and leave every round after the fourth of the season unplayed.
        var day = MatchDay.Create(Guid.NewGuid(), 7, new DateOnly(2026, 3, 7));
        var tolerance = TimeSpan.FromMinutes(30);

        Assert.False(day.IsDue(day.KickOffAt(CompetitionType.Cup), day.KickOffAt(CompetitionType.League), tolerance));
        Assert.True(day.IsDue(day.KickOffAt(CompetitionType.League), day.KickOffAt(CompetitionType.League), tolerance));
    }

    [Fact]
    public void The_championship_of_a_division_is_every_club_meeting_every_other_one_home_and_away()
    {
        // The identity the calendar rests on: thirty matchdays of eight fixtures each is
        // every pair of sixteen clubs twice, and no fewer. Change the number of clubs and
        // this stops holding, which is what it is here to notice.
        Assert.Equal(16, CompetitionRules.ClubsPerDivision);
        Assert.Equal(30, CompetitionRules.LeagueMatchDays);
        Assert.Equal(
            CompetitionRules.ClubsPerDivision * (CompetitionRules.ClubsPerDivision - 1),
            CompetitionRules.LeagueMatchDays * CompetitionRules.ClubsPerDivision / 2);
    }

    [Fact]
    public void A_season_starts_on_its_first_day_and_ends_on_its_rest_day()
    {
        // Thirty-four days: day one for the Supercup, thirty for the championship and the two
        // legs of the final, and one with nothing in it. The next season's day one is the day
        // after the rest day, so a season is a fixed run of days rather than a calendar year
        // that two seasons can be inside of.
        Assert.Equal(34, CompetitionRules.SeasonMatchDays);
        Assert.Equal(CompetitionRules.SeasonRestDay, CompetitionRules.SeasonMatchDays);
        Assert.True(CompetitionRules.SeasonRestDay > CompetitionRules.CupLegMatchDays(CompetitionRules.CupRounds).SecondLeg);
    }
}
