namespace NinjaEleven.Domain.Teams;

/// <summary>
/// Calculates match attendance based on club, match importance, and external factors.
/// All calculation rules live here so they can be tuned in one place.
/// </summary>
public static class AttendanceCalculator
{
    public const int StadiumCapacity = 5000;
    public const decimal TicketPrice = 10m;

    /// <summary>
    /// Base fill rate for a league match (before any modifiers).
    /// </summary>
    public const double BaseFillRate = 0.45;

    /// <summary>
    /// Maximum fill rate cap.
    /// </summary>
    public const double MaxFillRate = 0.95;

    /// <summary>
    /// Minimum fill rate floor.
    /// </summary>
    public const double MinFillRate = 0.15;

    /// <summary>
    /// Calculates attendance for a match.
    /// </summary>
    /// <param name="homeStadium">Home team's stadium</param>
    /// <param name="homeStars">Home team's star rating (0-5)</param>
    /// <param name="awayStars">Away team's star rating (0-5)</param>
    /// <param name="isDerby">Whether this is a local derby (same city/region)</param>
    /// <param name="matchImportance">Match importance multiplier (1.0 = regular, up to 2.0 = final)</param>
    /// <param name="weatherFactor">Weather factor (0.8 = rain, 1.0 = normal, 1.1 = perfect)</param>
    /// <param name="dayOfWeekFactor">Day of week factor (weekend = 1.1, weekday = 0.9)</param>
    /// <returns>Calculated attendance</returns>
    public static int Calculate(
        Stadium homeStadium,
        double homeStars,
        double awayStars,
        bool isDerby = false,
        double matchImportance = 1.0,
        double weatherFactor = 1.0,
        double dayOfWeekFactor = 1.0)
    {
        if (homeStadium == null)
        {
            return 0;
        }

        double fillRate = BaseFillRate;

        // Star power modifier: average of both teams' stars, scaled
        double avgStars = (homeStars + awayStars) / 2.0;
        fillRate *= 1.0 + (avgStars / 5.0) * 0.3; // Up to +30% for 5-star matchup

        // Derby bonus
        if (isDerby)
        {
            fillRate *= 1.25; // +25% for derbies
        }

        // Match importance
        fillRate *= matchImportance;

        // Weather
        fillRate *= weatherFactor;

        // Day of week
        fillRate *= dayOfWeekFactor;

        // Clamp
        fillRate = Math.Max(MinFillRate, Math.Min(MaxFillRate, fillRate));

        int attendance = (int)Math.Round(homeStadium.Capacity * fillRate);
        return Math.Min(attendance, homeStadium.Capacity);
    }

    /// <summary>
    /// Calculates gate revenue for a match.
    /// </summary>
    /// <param name="attendance">Calculated attendance</param>
    /// <returns>Revenue in limos</returns>
    public static decimal CalculateRevenue(int attendance)
    {
        return attendance * TicketPrice;
    }
}