using NinjaEleven.Domain.Matches;

namespace NinjaEleven.Application.Repositories;

public interface IFixtureRepository
{
    Task<IReadOnlyList<Fixture>> ListAsync(CancellationToken cancellationToken = default);
    Task<Fixture?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Fixture>> ListByRoundAsync(Guid roundId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every fixture of a set of rounds at once. A table is built from a season's fixtures and
    /// reading them round by round is a query per matchday to answer one question.
    /// </summary>
    Task<IReadOnlyList<Fixture>> ListByRoundIdsAsync(
        IEnumerable<Guid> roundIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One club's fixtures falling on one calendar day, across every competition in the world.
    ///
    /// <para>
    /// A fixture carries no date of its own: it has a window, a window has a matchday, and a
    /// matchday has a date. So the question "is this club playing today" is a walk of three
    /// tables, and this is that walk as a single reader. It is asked by the training allowance
    /// on every click, and answering it by reading the whole season's calendar to find the one
    /// day in question is a question about one club's day that costs every other club's season
    /// to answer.
    /// </para>
    ///
    /// <para>
    /// The day is a calendar day and not a window, because a cup tie and a league game on the
    /// same date are the same day to a squad that has to rest before either of them.
    /// </para>
    /// </summary>
    /// <param name="teamId">The club being asked about.</param>
    /// <param name="date">The calendar day in question.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<Fixture>> ListByTeamAndDateAsync(
        Guid teamId,
        DateOnly date,
        CancellationToken cancellationToken = default);

    Task AddAsync(Fixture fixture, CancellationToken cancellationToken = default);

    /// <summary>
    /// A whole round's fixtures at once. A season's calendar is two hundred and twenty
    /// fixtures and adding them one at a time is a round trip each for a decision that was
    /// already made.
    /// </summary>
    Task AddRangeAsync(IEnumerable<Fixture> fixtures, CancellationToken cancellationToken = default);

    void Update(Fixture fixture);

    /// <summary>
    /// Drops fixtures that were scheduled and never played.
    ///
    /// This exists for one caller: the calendar's own redraw. A season whose first draw was
    /// interrupted has fixtures that were never reached by a ball, and they are the reason the
    /// draw could not be finished. A fixture that has a match in it is never removed by this,
    /// because a played match is history rather than a leftover.
    /// </summary>
    void RemoveRange(IEnumerable<Fixture> fixtures);
}
