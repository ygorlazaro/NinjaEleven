using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Domain.Players;

/// <summary>
/// What one session of training costs, and what it is allowed to do.
///
/// <para>
/// Training spends energy and gets a point, and the interesting number is the price of the
/// point rather than the size of it. A flat price would make a player's development
/// something a manager settles in one afternoon — a hundred energy buys the same number of
/// points whether the man is nineteen or thirty-four, so the first session of the season
/// would be the last one that mattered and the rest of the calendar would be a formality. A
/// price that climbs towards the ceiling makes the same energy buy a great deal of a young
/// man's improvement and almost nothing of a veteran's, which is the trade the manager is
/// actually making: spend now on the legs that will still be there in a decade, or spend
/// later on the technique that will not.
/// </para>
///
/// <para>
/// The price is squared rather than linear because a linear ramp still pays a man half his
/// potential for his effort, and the square is what makes the last ten points of a career
/// cost a season's whole energy budget. The band around it is deliberately narrow in
/// absolute terms — the most expensive session in the game is a third of a player's energy,
/// so a training programme is a budgeting decision inside a season rather than a button that
/// ends a man's season.
/// </para>
/// </summary>
public static class TrainingRules
{
    /// <summary>The cheapest session there is: a young man with a long way to go.</summary>
    public const int MinimumCost = 6;

    /// <summary>
    /// The dearest session there is: a player training an attribute that is already at his
    /// potential. It is a third of the whole scale, so the deepest point on any player is
    /// three sessions' worth of a season's budget rather than one that empties him.
    /// </summary>
    public const int MaximumCost = 34;

    /// <summary>
    /// What one session on this attribute costs this man in energy, rounded up so a price is
    /// never a fraction of a point a manager is spending whole.
    /// </summary>
    /// <remarks>
    /// The stamina multiplier is the tank from <see cref="StaminaRules"/> and not a new
    /// number: a man who empties easily also tires of training, and reusing the one that
    /// already decides what a match costs him is the reason the two cannot disagree.
    /// </remarks>
    public static int Cost(Player player, PlayerAttribute attribute)
    {
        ArgumentNullException.ThrowIfNull(player);

        var fraction = Math.Clamp(player.Get(attribute) / (double)player.Potential, 0.0, 1.0);
        var baseCost = MinimumCost + (MaximumCost - MinimumCost) * fraction * fraction;

        return (int)Math.Ceiling(baseCost * StaminaRules.TankCost(player.Stamina));
    }

    /// <summary>
    /// Whether a session on this attribute would take him past his potential, which is the
    /// one thing training is never allowed to do.
    ///
    /// <para>
    /// The ceiling is the potential itself rather than a share of the way to it. A man
    /// cannot be trained past what he is capable of, and a rule that let a manager grind any
    /// player to a hundred would make the world's one number for a man a manager's decision
    /// instead of a fact about him — which is the difference between a football manager and
    /// a cheat.
    /// </para>
    /// </summary>
    public static bool IsAtCeiling(Player player, PlayerAttribute attribute)
    {
        ArgumentNullException.ThrowIfNull(player);

        return player.Get(attribute) >= player.Potential;
    }

    /// <summary>
    /// The sessions a club has on a day it is playing on.
    ///
    /// <para>
    /// One, and it is the number rather than zero because a matchday is not a day off. A
    /// squad that has eleven men on the pitch at three o'clock still has twelve on the bench
    /// and a squad that trained them before kick-off would have spent the morning on the
    /// training ground and the afternoon on the one thing they were saved for. The session is
    /// affordable here; it is simply not free, and the price of it is the same price as ever.
    /// </para>
    /// </summary>
    public const int SessionsOnAMatchDay = 1;

    /// <summary>
    /// The sessions a club has on a day it is not playing on.
    ///
    /// <para>
    /// Two, so a rest day is a doubling rather than a windfall. The day a club does not play
    /// is the day its reserve side plays, and the men who would have started that afternoon
    /// are the men who are here to be worked; a squad given four sessions would be a squad
    /// whose second-choice goalkeeper is as trained as its first-choice striker by Tuesday.
    /// </para>
    /// </summary>
    public const int SessionsOnARestDay = 2;

    /// <summary>
    /// What one session costs the club, as a share of the man's season wage.
    ///
    /// <para>
    /// Energy alone is a price paid in the wrong currency. Every club in the world draws
    /// from the same pool of energy and the same pool of men, so energy prices a session
    /// against the pitch and never against the budget — a manager who trained his whole
    /// squad every day of a season would simply arrive at every matchday with a tired team,
    /// and the man who spends nothing is not saving anything. Money is the only thing in the
    /// game that belongs to the club rather than to the player, so it is the only thing that
    /// can price development for a manager who has a board.
    /// </para>
    ///
    /// <para>
    /// Fifteen per cent of a season's wage is a real budget decision and not a rounding
    /// error. A squad of twenty-three carries about L$ 162,000 of wages across a season, and a
    /// manager who spends both sessions on the same man on every day of the calendar pays
    /// about L$ 72,000 for the privilege — some forty per cent on top of the wage bill, and
    /// nothing at all to a club that would rather not develop anybody. It is high enough that
    /// the choice is a real one, and low enough that a club which trains deliberately is not
    /// bankrupt by the end of the month.
    /// </para>
    ///
    /// <para>
    /// It is a share of the season wage and not of the per-matchday instalment because the
    /// season wage is the number a manager is shown. Pricing a session against an instalment
    /// would be a fee the manager can see nowhere on any screen, which is the one thing a
    /// cost is not allowed to be.
    /// </para>
    /// </summary>
    public const decimal SessionFeeRate = 0.15m;

    /// <summary>
    /// How many sessions a club has left to spend on a day, which is what the day turns on.
    /// </summary>
    /// <param name="hasMatch">Whether the club has a fixture on the day in question.</param>
    public static int DailyBudget(bool hasMatch) => hasMatch ? SessionsOnAMatchDay : SessionsOnARestDay;

    /// <summary>
    /// What one session on one man costs the club, being a share of his season wage.
    /// </summary>
    /// <param name="seasonWage">The man's season wage, as the squad screen shows it.</param>
    public static decimal SessionFee(decimal seasonWage)
    {
        if (seasonWage < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(seasonWage), seasonWage, "A wage is not a debt.");
        }

        return decimal.Round(seasonWage * SessionFeeRate, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Whether this attribute is one this man has. A centre-back has no reflexes to train,
    /// and the check is here rather than in the service so the rule and the reason for it
    /// travel together.
    ///
    /// <para>
    /// A goalkeeper passes for every attribute, including the five outfield ones: they are
    /// on his card and the engine reads them in a squad reshuffle, so a club that has spent
    /// money signing a keeper who can also head should not find the game silently refusing
    /// to let him improve at it. Only the reverse is refused.
    /// </para>
    /// </summary>
    public static bool BelongsToThisMan(Player player, PlayerAttribute attribute)
    {
        ArgumentNullException.ThrowIfNull(player);

        return player.Position == Position.GK
            || attribute is not (PlayerAttribute.GoalkeeperPower or PlayerAttribute.Reflexes);
    }
}
