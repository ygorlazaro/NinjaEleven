using NinjaEleven.Domain.Players;

namespace NinjaEleven.Domain.Competitions;

/// <summary>
/// Calculates club strength from the full squad. Used for initial division assignment,
/// user club auto-assignment, and other strength-based decisions.
/// </summary>
public static class ClubStrength
{
    /// <summary>
    /// Calculates the strength of a club from its full squad (all players, not just starters).
    /// Returns the average star rating of all players in the squad.
    /// </summary>
    public static double Calculate(IReadOnlyList<Player> squad)
    {
        if (squad is null || squad.Count == 0)
        {
            return 0;
        }

        var total = 0.0;
        foreach (var player in squad)
        {
            total += PlayerRating.CalculateStars(player);
        }

        return Math.Round(total / squad.Count, 2);
    }

    /// <summary>
    /// Calculates the strength of a club from player season states (for a specific season).
    /// </summary>
    public static double CalculateFromSeasonStates(
        IReadOnlyList<Player> players,
        IReadOnlyDictionary<Guid, PlayerSeasonState> seasonStates)
    {
        if (players is null || players.Count == 0)
        {
            return 0;
        }

        var total = 0.0;
        var count = 0;

        foreach (var player in players)
        {
            if (seasonStates.TryGetValue(player.Id, out var state) && state.TeamId.HasValue)
            {
                total += PlayerRating.CalculateStars(player);
                count++;
            }
        }

        return count > 0 ? Math.Round(total / count, 2) : 0;
    }

    /// <summary>
    /// Gets the maximum individual star rating in a squad.
    /// </summary>
    public static double GetMaxPlayerStars(IReadOnlyList<Player> squad)
    {
        if (squad is null || squad.Count == 0)
        {
            return 0;
        }

        var max = 0.0;
        foreach (var player in squad)
        {
            var stars = PlayerRating.CalculateStars(player);
            if (stars > max)
            {
                max = stars;
            }
        }

        return max;
    }

    /// <summary>
    /// Counts how many players in the squad have a specific star rating.
    /// </summary>
    public static int CountPlayersWithStars(IReadOnlyList<Player> squad, double stars)
    {
        if (squad is null)
        {
            return 0;
        }

        var count = 0;
        foreach (var player in squad)
        {
            if (Math.Abs(PlayerRating.CalculateStars(player) - stars) < 0.01)
            {
                count++;
            }
        }

        return count;
    }
}