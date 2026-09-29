using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Domain.Players;

/// <summary>
    /// Calculates player and team strength as star ratings. The conversion lives in the
    /// domain so the same numbers are used by the match engine, the lineup suggestion and
    /// every screen that shows a player's quality. A star is never computed in the client.
    /// </summary>
    public static class PlayerRating
    {
        /// <summary>
        /// Converts a single attribute value (1..100) to stars (0.5..5.0).
        /// Formula: ceil(attribute / 10) * 0.5
        /// 1-10 → 0.5, 11-20 → 1.0, ..., 91-100 → 5.0
        /// </summary>
        public static double AttributeToStars(int attribute)
        {
            if (attribute <= 0) return 0;
            
            var stars = Math.Ceiling(attribute / 10.0) * 0.5;
            return Math.Min(stars, 5.0);
        }

    /// <summary>
    /// Calculates stars from raw attribute values for an outfield player.
    /// </summary>
    public static double CalculateOutfieldStars(int speed, int accuracy, int dribbling, int heading, int strength)
    {
        var sum = AttributeToStars(speed)
            + AttributeToStars(accuracy)
            + AttributeToStars(dribbling)
            + AttributeToStars(heading)
            + AttributeToStars(strength);

        var average = sum / 5.0;
        return RoundToHalfStar(average);
    }

    /// <summary>
    /// Calculates stars from raw attribute values for a goalkeeper.
    /// </summary>
    public static double CalculateGoalkeeperStars(int speed, int accuracy, int goalkeeperPower, int reflexes, int strength)
    {
        var sum = AttributeToStars(speed)
            + AttributeToStars(accuracy)
            + AttributeToStars(goalkeeperPower)
            + AttributeToStars(reflexes)
            + AttributeToStars(strength);

        var average = sum / 5.0;
        return RoundToHalfStar(average);
    }

    /// <summary>
    /// Calculates the star rating for a single player (0..5, in 0.5 increments).
    /// </summary>
    public static double CalculateStars(Player player)
    {
        if (player is null) throw new ArgumentNullException(nameof(player));

        return player.Position == Position.GK
            ? CalculateGoalkeeperStars(player.Speed, player.Accuracy, player.GoalkeeperPower, player.Reflexes, player.Strength)
            : CalculateOutfieldStars(player.Speed, player.Accuracy, player.Dribbling, player.Heading, player.Strength);
    }

    /// <summary>
    /// Calculates the star rating for a match player snapshot (0..5, in 0.5 increments).
    /// Uses the same attribute set as the player entity.
    /// </summary>
    public static double CalculateStars(Matches.MatchPlayerSnapshot snapshot)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));

        return snapshot.Position == Position.GK
            ? CalculateGoalkeeperStars(snapshot.Speed, snapshot.Accuracy, snapshot.GoalkeeperPower, snapshot.Reflexes, snapshot.Strength)
            : CalculateOutfieldStars(snapshot.Speed, snapshot.Accuracy, snapshot.Dribbling, snapshot.Heading, snapshot.Strength);
    }

    /// <summary>
    /// Calculates the star rating for a club: the average of all players in the squad.
    /// Includes injured, suspended and sent-off players — it represents the full
    /// squad depth, not the current eleven.
    /// </summary>
    public static double CalculateTeamStars(IReadOnlyList<Player> squad, IReadOnlyDictionary<Guid, Matches.MatchPlayerSnapshot>? snapshots = null)
    {
        if (squad is null || squad.Count == 0) return 0;

        var total = 0.0;
        var count = 0;

        foreach (var player in squad)
        {
            double stars;
            if (snapshots != null && snapshots.TryGetValue(player.Id, out var snapshot))
            {
                stars = CalculateStars(snapshot);
            }
            else
            {
                stars = CalculateStars(player);
            }
            total += stars;
            count++;
        }

        if (count == 0) return 0;

        var average = total / count;
        return RoundToHalfStar(average);
    }

    /// <summary>
    /// Calculates the star rating for a club from match player snapshots (e.g. during a match).
    /// Includes lineup and bench — the full available squad for that match.
    /// </summary>
    public static double CalculateTeamStarsFromSnapshots(IReadOnlyList<Matches.MatchPlayerSnapshot> allPlayers)
    {
        if (allPlayers is null || allPlayers.Count == 0) return 0;

        var total = 0.0;
        foreach (var snapshot in allPlayers)
        {
            total += CalculateStars(snapshot);
        }

        var average = total / allPlayers.Count;
        return RoundToHalfStar(average);
    }

    /// <summary>
    /// Rounds a value to the nearest 0.5 increment, clamped to 0..5.
    /// </summary>
    private static double RoundToHalfStar(double value)
    {
        var rounded = Math.Round(value * 2, MidpointRounding.AwayFromZero) / 2.0;
        return Math.Clamp(rounded, 0, 5.0);
    }

    /// <summary>
    /// Returns a string representation of stars using Unicode star characters.
    /// e.g., 3.5 -> "★★★½", 4.0 -> "★★★★"
    /// </summary>
    public static string ToStarString(double stars)
    {
        var full = (int)Math.Floor(stars);
        var hasHalf = stars - full >= 0.5;
        var result = new string('★', full);
        if (hasHalf) result += "½";
        return result;
    }
}