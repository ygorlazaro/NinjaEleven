namespace NinjaEleven.Domain.Managers;

/// <summary>
/// How long a club may wait for a manager who has stopped showing up.
///
/// The number lives here, with the other rules of the game, rather than in the service that
/// enforces it: a threshold written inside the sweep is a threshold nobody can find when the
/// world stops and nobody can answer why a club changed hands.
/// </summary>
public static class ManagerDormancy
{
    /// <summary>
    /// A manager who has not signed in for this long is dismissed and the club goes back to
    /// the world.
    ///
    /// Thirty days is a season's worth of silence in a game whose football is a season, so the
    /// club is not handed over for a week away and not held for a year away. It is long
    /// enough that a manager on holiday keeps his chair, and short enough that a club is never
    /// left waiting on a person who has gone.
    /// </summary>
    public static readonly TimeSpan MaxDaysAway = TimeSpan.FromDays(30);
}
