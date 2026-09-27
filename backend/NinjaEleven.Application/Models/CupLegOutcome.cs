using NinjaEleven.Domain.Matches;

namespace NinjaEleven.Application.Models;

/// <summary>
/// A cup tie's leg, as it finished.
///
/// This is what the cup needs to hear about a match and nothing else. The service is not handed
/// a <see cref="MatchState"/> and a fixture id because the cup is not a match: it is two
/// matches, and what it has to know is the score of each and who was out there to take the
/// penalties. Handing it the whole engine state would be handing it the means to re-decide the
/// match, and the cup must never be able to contradict the result of a game somebody watched.
/// </summary>
public sealed record CupLegOutcome
{
    public required Guid FixtureId { get; init; }

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
    /// The men who would take the penalties, and the keeper each of them would face, keyed by
    /// club. Keyed by club rather than by side on purpose: the two legs swap ends, so "home"
    /// means one club in the first leg and the other in the second, and a shootout taken at the
    /// end of a tie is taken by the same two clubs whoever was at home.
    /// </summary>
    public required IReadOnlyDictionary<Guid, PenaltyTaker> Takers { get; init; }
}

/// <summary>
/// A side's penalty taker and the keeper he is up against.
/// </summary>
/// <param name="Taker">The man in front of the ball.</param>
/// <param name="Keeper">The man in goal, or null when the other side has nobody in it.</param>
public sealed record PenaltyTaker(MatchPlayerSnapshot Taker, MatchPlayerSnapshot? Keeper);
