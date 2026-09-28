namespace NinjaEleven.Domain.Transfers;

/// <summary>
/// The order the market is read in: fixed, and looking like nobody's idea of alphabetical.
///
/// A manager looking for a striker does not want the striker called Adriano first, and a
/// market sorted by value is a market that only ever shows the same twenty names. Random is
/// the right answer, but random that is drawn again on every call is worse than useless: the
/// second page of a result is not the continuation of the first, a man who fits the filters
/// cannot be found because he moved, and a club looking at the same market twice sees two
/// different leagues.
///
/// So the shuffle is a property of the player rather than of the request. Every player's
/// place is worked out from his own identifier by a hash that is fixed for the life of the
/// world, and sorting by it is a shuffle that never moves. It is not a random number
/// generator: it is the same function over the same bytes every time, in this process and in
/// the next one, which is the only kind of randomness a paginated list can be built on.
/// </summary>
public static class MarketOrder
{
    /// <summary>
    /// The fixed place a player holds in the market, for ever. Two players never share it, and
    /// the same player always gets the same one — which is what makes page two of a search the
    /// same page two on every call.
    /// </summary>
    public static int KeyOf(Guid playerId)
    {
        // FNV-1a, 32 bits, over the identifier's sixteen bytes. It is a hash and not a random
        // draw on purpose: the answer must be the same tomorrow and in the next process, or
        // the market would reshuffle under a manager who is halfway down a list.
        unchecked
        {
            var bytes = playerId.ToByteArray();
            uint hash = 2166136261;

            foreach (var value in bytes)
            {
                hash ^= value;
                hash *= 16777619;
            }

            return (int)hash;
        }
    }
}
