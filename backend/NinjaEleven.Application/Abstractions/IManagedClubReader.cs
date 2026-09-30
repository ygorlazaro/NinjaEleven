namespace NinjaEleven.Application.Abstractions;

/// <summary>
/// The clubs a person is in charge of.
/// </summary>
public interface IManagedClubReader
{
    /// <summary>
    /// Reads the clubs that have a manager with a person behind it, rather than one the world
    /// invented for the transfer market.
    ///
    /// <para>
    /// It is asked of the world and not of the request because a club somebody manages is a
    /// fact about the world, not about the call that happens to walk it: the scheduler and a
    /// hand pressing a button are both walking the same season, and both owe the same thing to
    /// the man whose match is in it. A window that simulates a game somebody is waiting to
    /// watch is a game he never played — and that has to be true of every way of moving the
    /// world forward, not only of the one a screen happens to use.
    /// </para>
    /// </summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The clubs with a person behind them; empty in a world of nobody.</returns>
    Task<IReadOnlyList<Guid>> ListManagedClubsAsync(CancellationToken cancellationToken = default);
}
