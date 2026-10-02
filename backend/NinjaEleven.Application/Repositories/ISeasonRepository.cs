using NinjaEleven.Domain.Seasons;

namespace NinjaEleven.Application.Repositories;

public interface ISeasonRepository
{
    Task<IReadOnlyList<Season>> ListAsync(CancellationToken cancellationToken = default);
    Task<Season?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The season with a given number. A season is identified by the number it was given and
    /// not by the year it fell in, so "the season after this one" is a lookup by number.
    /// </summary>
    Task<Season?> GetByNumberAsync(int number, CancellationToken cancellationToken = default);

    Task<Season?> GetCurrentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// How many rounds of a season's championship are still to be played.
    /// </summary>
    /// <remarks>
    /// A release settlement counts the rounds a club still has to pay for, so the squad list, a
    /// player's card and the release command all need it — and all three of them already hold
    /// a season. It is asked of the season rather than of a service because it is a fact about
    /// the calendar and not about any one club: every club in a division has the same rounds
    /// left, so it is read once for a squad instead of once per man.
    /// <para>
    /// A round counts as played when its window is closed, which means every one of its
    /// fixtures is finished. A date that has passed is not the same thing: a season left
    /// running over a weekend would answer the two questions differently, and only one of them
    /// is about football.
    /// </para>
    /// </remarks>
    Task<int> GetChampionshipRoundsLeftAsync(Guid seasonId, CancellationToken cancellationToken = default);
    Task AddAsync(Season season, CancellationToken cancellationToken = default);
    void Update(Season season);
}
