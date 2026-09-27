using NinjaEleven.Domain.Competitions;

namespace NinjaEleven.Application.Models;

/// <summary>
/// A cup tie's leg, as it finished.
///
/// This is what the cup needs to hear about a match and nothing else. The service is not handed
/// a <see cref="MatchState"/> and a fixture id because the cup is not a match: it is two
/// matches, and what it has to know is the score of each and how the shootout that settled it
/// ended. Handing it the whole engine state would be handing it the means to re-decide the
/// match, and the cup must never be able to contradict the result of a game somebody watched.
/// </summary>
public sealed record CupLegOutcome
{
    public required Guid FixtureId { get; init; }

    /// <summary>Who was at home in this leg.</summary>
    public required Guid HomeTeamId { get; init; }

    /// <summary>Who was away in this leg — the other club.</summary>
    public required Guid AwayTeamId { get; init; }

    /// <summary>Goals by the club that was at home in this leg.</summary>
    public required int HomeScore { get; init; }

    /// <summary>Goals by the club that was away in this leg.</summary>
    public required int AwayScore { get; init; }

    /// <summary>
    /// The seed of the match that just finished. A shootout is drawn from this rather than
    /// from a fresh source, so a tie that is replayed takes the same penalties in the same
    /// order — the same rule the rest of the engine keeps.
    /// </summary>
    public required int Seed { get; init; }

    /// <summary>
    /// The shootout this leg went to, counted by this leg's sides, or null when it did not
    /// go to one.
    ///
    /// It is the shootout the match played in front of the crowd, with the two managers'
    /// own orders of takers, rather than anything the cup works out for itself afterwards, and
    /// it counts this leg's sides rather than the tie's clubs, because the two legs swap ends
    /// and the service that reads this back is the one that knows the swap. A cup that decided
    /// a level tie by a rule of its own would be a cup whose penalties nobody in the stadium
    /// had watched, and the eleven that played the tie would not be the eleven that won it.
    /// </summary>
    public ShootoutOutcome? Shootout { get; init; }
}
