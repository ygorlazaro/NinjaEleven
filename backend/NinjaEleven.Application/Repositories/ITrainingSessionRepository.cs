using NinjaEleven.Domain.Players;

namespace NinjaEleven.Application.Repositories;

/// <summary>
/// The history of training, read as a history.
/// </summary>
public interface ITrainingSessionRepository
{
    /// <summary>
    /// The sessions one club has already run on one day, which is what the daily allowance is
    /// counted from.
    ///
    /// <para>
    /// A count read from the sessions themselves rather than from a counter that was
    /// incremented: a counter is a second answer to the same question, and the two can only
    /// be as right as the moment they were last written. The sessions are the answer, and a
    /// club that has spent its allowance is a club with rows in this list.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<TrainingSession>> ListByTeamAndDayAsync(
        Guid teamId,
        DateOnly day,
        CancellationToken cancellationToken = default);

    /// <summary>A player's own training history, most recent first.</summary>
    Task<IReadOnlyList<TrainingSession>> ListByPlayerAsync(
        Guid playerId,
        int take = 20,
        CancellationToken cancellationToken = default);

    Task AddAsync(TrainingSession session, CancellationToken cancellationToken = default);
}
