namespace NinjaEleven.Domain.Seasons;

/// <summary>
/// A tier of the pyramid: the 1st division, the 2nd, the 3rd.
///
/// A division is permanent and not part of a season. Which clubs are in it changes every
/// season, and that is kept by the participants of the division's competition season rather
/// than by anything on the club: a club's place in the pyramid is true for one season, while
/// the division itself outlives every club in it.
/// </summary>
public class Division
{
    public Guid Id { get; private set; }

    /// <summary>"1ª Divisão", "2ª Divisão", "3ª Divisão".</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>1 is the top of the pyramid. A higher number is a lower division.</summary>
    public int Tier { get; private set; }

    private Division() { }

    public static Division Create(string? name, int tier)
    {
        if (tier < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tier),
                tier,
                "The first division is tier 1; there is no division above it.");
        }

        return new Division
        {
            Id = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(name) ? Competitions.CompetitionRules.DivisionName(tier) : name.Trim(),
            Tier = tier
        };
    }

    /// <summary>
    /// The tier above, or null when this is already the top of the pyramid. Promotion and
    /// relegation are the same movement seen from two tiers, which is why they are asked for
    /// as neighbours rather than as two separate rules.
    /// </summary>
    public int? TierAbove => Tier > 1 ? Tier - 1 : null;

    /// <summary>The tier below, or null when there is no division below this one.</summary>
    public int? TierBelow => Tier < Competitions.CompetitionRules.DivisionCount ? Tier + 1 : null;

    public override string ToString() => Name;
}
