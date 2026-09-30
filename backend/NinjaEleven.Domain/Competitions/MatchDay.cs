using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Domain.Competitions;

/// <summary>
/// A day of the season's calendar: a number, a date, and the windows of football in it.
/// </summary>
public class MatchDay
{
    /// <summary>
    /// The world's offset from UTC, and therefore the one a matchday's dates and kick-off
    /// times are read in. It is fixed rather than configurable so a date in the calendar has
    /// exactly one meaning: a season drawn in one run and drawn in another puts its matchdays
    /// on the same afternoon, and the world does not move its own kick-off because a
    /// deployment happened in a different timezone.
    /// </summary>
    public static readonly TimeSpan CalendarOffset = TimeSpan.Zero;

    public Guid Id { get; private set; }
    public Guid SeasonId { get; private set; }

    /// <summary>Counted from one. Matchday 1 is the first day of the season.</summary>
    public int Number { get; private set; }

    /// <summary>
    /// The date this matchday falls on. It is the season's start date plus its own offset,
    /// so a matchday is a day of the calendar rather than a day of the process.
    /// </summary>
    public DateOnly Date { get; private set; }

    private MatchDay() { }

    public static MatchDay Create(Guid seasonId, int number, DateOnly date)
    {
        if (number < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(number), number, "A matchday is counted from one.");
        }

        return new MatchDay
        {
            Id = Guid.NewGuid(),
            SeasonId = seasonId,
            Number = number,
            Date = date
        };
    }

    /// <summary>
    /// The instant a window of this matchday goes out.
    ///
    /// The date is the world's and the hour is the competition's, so a matchday answers
    /// "when does the cup of this day kick off" without anybody having to remember that the
    /// cup is in the evening. This is the one place the two halves of a matchday's schedule
    /// are put together, which is what makes it the one place they can be read.
    /// </summary>
    /// <param name="wave">Which window of the day.</param>
    public DateTimeOffset KickOffAt(CompetitionType wave) =>
        new DateTimeOffset(Date.ToDateTime(CompetitionRules.KickOffTimeOf(wave), DateTimeKind.Unspecified), CalendarOffset);

    /// <summary>
    /// Whether a window that goes out at the given instant should have gone out by now.
    ///
    /// A matchday that is behind counts as due rather than being skipped: a world that was
    /// down over a weekend has matchdays nobody kicked off, and the next run of the Scheduler
    /// plays them rather than leaving the season a day behind its own results for ever.
    ///
    /// <para>
    /// The instant is passed in rather than worked out here, and that is the whole shape of
    /// the rule. A matchday knows its own <em>date</em>; it does not know what that date is
    /// worth on the clock the world is running, which is a day under the calendar's own dates
    /// and ten minutes in a development world that has been told to hurry. The world decides
    /// what an instant means and the matchday decides whether it has arrived, so a rule about
    /// timing and a setting about speed cannot be confused for one another.
    /// </para>
    ///
    /// <para>
    /// The tolerance reaches backwards as well as forwards, and that is deliberate: the poll
    /// that asks this question runs on a clock of its own, and a process that started at
    /// nineteen twenty-five should not sleep fifteen minutes waiting for the hour it is
    /// already inside. The cost is that a window can go out a few minutes early, which for a
    /// world whose windows are hours apart is a smaller thing than a window that goes out
    /// late because nobody was awake to fire it.
    /// </para>
    /// </summary>
    /// <param name="goesOutAt">The real instant this window goes out.</param>
    /// <param name="now">The instant being asked about.</param>
    /// <param name="tolerance">How far either side of the hour the window may be run.</param>
    public bool IsDue(DateTimeOffset goesOutAt, DateTimeOffset now, TimeSpan tolerance) =>
        goesOutAt - tolerance <= now;
}
