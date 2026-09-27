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
