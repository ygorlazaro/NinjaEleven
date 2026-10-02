using NinjaEleven.Domain.Competitions;

namespace NinjaEleven.Application.Models;

/// <summary>
/// A season's calendar, as a manager reads it: when the football is, and what is in it.
///
/// A season is not a list of matches, it is a list of days with football on them. Asking
/// "when does this club play" and getting a date back is a different question from "which
/// fixtures are in the second window of matchday twelve", and one shape that carries the
/// matchdays and the windows scheduled on them answers both without either being derived
/// from the other on the client.
/// </summary>
public class SeasonCalendar
{
    public required Guid SeasonId { get; init; }
    public required string SeasonName { get; init; }

    /// <summary>The days of the season, in order.</summary>
    public IReadOnlyList<MatchDay> MatchDays { get; init; } = Array.Empty<MatchDay>();

    /// <summary>Every window of football scheduled anywhere in the season.</summary>
    public IReadOnlyList<Round> Windows { get; init; } = Array.Empty<Round>();

    /// <summary>
    /// The day the world is on: the first day of the season that still has a fixture nobody
    /// has played. Null when the season has not been drawn yet.
    ///
    /// <para>
    /// It is a fact about the football and not about the wall clock. A season is thirty-four
    /// days on the calendar and however long it takes on the machine, so the date a matchday
    /// carries can be weeks away from the day it is actually played — which is why a calendar
    /// that worked out "today" from the date would open on day one of a season the world is
    /// twenty days into.
    /// </para>
    /// </summary>
    public int? CurrentMatchDayNumber { get; init; }

    public int MatchDayCount => MatchDays.Count;

    public MatchDay? MatchDayOf(int number) =>
        MatchDays.FirstOrDefault(matchDay => matchDay.Number == number);
}
