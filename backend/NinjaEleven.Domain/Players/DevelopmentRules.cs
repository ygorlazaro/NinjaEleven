using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;

namespace NinjaEleven.Domain.Players;

/// <summary>
/// How a player moves through a career: what he is worth now, what he is worth at his best,
/// and how the eight things about him get there.
///
/// <para>
/// The design question was whether growth should be per-attribute or on the player as a
/// whole, and the answer had to be the second one. A model that grew each attribute towards
/// its own ceiling produced a centre-back with ninety-eight finishing at twenty-eight,
/// because finishing's own ceiling was as far away as his heading's. Per-attribute ceilings
/// need a reason for an attribute to stop, and "he is a defender" is not one a rating can
/// express. So there is one number — <see cref="Player.Potential"/> — which is the ceiling
/// on the <em>reading</em> of a man, and growth is spent against that reading rather than
/// against eight private ceilings that do not exist.
/// </para>
///
/// <para>
/// The reading is not a sum. It is the weighted one from <see cref="AttributeWeights"/>, so
/// a keeper's rating is his keeping and a striker's is his finishing, and a defender's
/// improvement is spent on heading and strength at thirty-four per cent each rather than
/// spread evenly across five things the engine does not read him on.
/// </para>
///
/// <para>
/// The one number this file cannot be given by the team is <b>how much of the remaining
/// room is spent in a year</b>, and the shape below was chosen by running it rather than by
/// picking a constant. A sixteen-year-old drawn at forty with a potential of eighty-eight
/// reaches the low seventies by twenty-four and turns over from there; a twenty-four-year-old
/// drawn at fifty-five with a potential of seventy-five reaches the high sixties. The first
/// is a prodigy arriving early and the second is a late developer, and the only difference
/// between them is how much room they had and how far their peaks were away — which is the
/// claim the whole file is making.
/// </para>
/// </summary>
public static class DevelopmentRules
{
    /// <summary>
    /// The age a man's growth begins. Below it a body is not yet a footballer's, and the
    /// retirement rule's youngest age is the same number for the same reason: one boundary,
    /// read by both rules.
    /// </summary>
    public const int YoungestAge = 16;

    // --- The peaks ----------------------------------------------------------------
    //
    // Each attribute is a different job and peaks at a different age, and the spread is the
    // point: a game where everything peaked at the same moment would have no reason to
    // rotate anybody, and the spread is also what makes a thirty-one-year-old a man the
    // engine measures differently from the same man at twenty-five.

    /// <summary>Pace goes first and goes earliest — a sprinter's asset is a young man's.</summary>
    public const int SpeedPeakAge = 24;

    /// <summary>
    /// Finishing peaks later than pace and far later than the game it is played in, which
    /// is the whole reason an old striker is still a threat.
    /// </summary>
    public const int AccuracyPeakAge = 27;

    /// <summary>Carrying the ball is a young man's work and fades with the pace that fed it.</summary>
    public const int DribblingPeakAge = 25;

    /// <summary>Late, because it is learned rather than grown.</summary>
    public const int HeadingPeakAge = 28;

    /// <summary>Between pace and the head: a body at its most useful to a team.</summary>
    public const int StrengthPeakAge = 26;

    /// <summary>
    /// A keeper's peak is the latest in the game by a distance. A goalkeeper's work is
    /// technique and positioning on a body that has stopped being the point, which is why
    /// the decline below is the gentlest of the eight and the floor the highest.
    /// </summary>
    public const int GoalkeeperPowerPeakAge = 30;

    /// <summary>Reflexes are the one thing that stays, and the decline says so.</summary>
    public const int ReflexesPeakAge = 30;

    /// <summary>
    /// The tank peaks a little before the body it is carried on. A man of twenty-six is
    /// stronger than a man of twenty-four and has less in him, which is exactly the trade
    /// the engine is measuring when a squad is rotated.
    /// </summary>
    public const int StaminaPeakAge = 26;

    // --- The decline --------------------------------------------------------------
    //
    // A decline is a share of the distance still left to the floor, not a flat number of
    // points a year, and that is deliberate for one reason: a flat decline walks every
    // attribute to the bottom of the scale and stays there. A man of thirty-eight would be
    // a one out of ten at everything, and the engine would read him as the worst player in
    // the division rather than as an old man who is worse than he was — which are different
    // sentences, and only the second one is true. A share of the remaining distance slows
    // as it goes, so he settles onto a floor and stays a recognisable player.

    /// <summary>How much faster the decline is for every year past the peak.</summary>
    public const double DeclineAcceleration = 0.15;

    // --- The growth ---------------------------------------------------------------

    /// <summary>
    /// How much of the room a man has left is spent in a year at full momentum — that is,
    /// at the very start of his career with his whole ceiling ahead of him.
    ///
    /// <para>
    /// It is applied to the <em>headroom</em> rather than added, which is what makes the
    /// curve decelerate on its own: a man with fifty points of room spends a fifth of what
    /// a man with ten does, and by the time the room is small the year rate barely moves
    /// him. A constant number of points a year instead would have every player in the
    /// world arriving at the same overall at the same age, with their potential doing
    /// nothing but deciding which of them stopped.
    /// </para>
    /// </summary>
    public const double YearRate = 0.20;

    /// <summary>
    /// The tank a body fills to, which is below the top of the scale on purpose. Nobody
    /// has a hundred in the tank, and a curve that ran to the ceiling would put a
    /// thirty-year-old in the same place as a man of nineteen with an exceptional body.
    /// </summary>
    public const double StaminaCeiling = 92;

    /// <summary>
    /// How much of the rest of the tank a man fills in a year at full momentum.
    ///
    /// <para>
    /// The rate is set by where it puts the peak, not by how fast it looks. The fill is
    /// exponential and the momentum decays towards the peak, so a slow rate can never arrive:
    /// at a sixth, a body entering at fifty reached seventy-two and stopped, and
    /// <see cref="StaminaCeiling"/> was a number the world could never reach and the seeder's
    /// bands were drawn above it — a constant chosen by feel which quietly made the ceiling
    /// dead. This one puts a young body at about eighty-eight out of ninety-two, so the
    /// ceiling is a target that is nearly reached rather than a wall nothing ever meets.
    /// </para>
    /// </summary>
    public const double StaminaYearRate = 0.45;

    /// <summary>Every attribute, in the order the engine would list them.</summary>
    public static readonly IReadOnlyList<PlayerAttribute> All = new[]
    {
        PlayerAttribute.Speed,
        PlayerAttribute.Accuracy,
        PlayerAttribute.Dribbling,
        PlayerAttribute.Heading,
        PlayerAttribute.Strength,
        PlayerAttribute.GoalkeeperPower,
        PlayerAttribute.Reflexes,
        PlayerAttribute.Stamina
    };

    /// <summary>The age this attribute is at its best.</summary>
    public static int PeakAge(PlayerAttribute attribute) => attribute switch
    {
        PlayerAttribute.Speed => SpeedPeakAge,
        PlayerAttribute.Accuracy => AccuracyPeakAge,
        PlayerAttribute.Dribbling => DribblingPeakAge,
        PlayerAttribute.Heading => HeadingPeakAge,
        PlayerAttribute.Strength => StrengthPeakAge,
        PlayerAttribute.GoalkeeperPower => GoalkeeperPowerPeakAge,
        PlayerAttribute.Reflexes => ReflexesPeakAge,
        _ => StaminaPeakAge
    };

    /// <summary>The share of the remaining distance a year past the peak takes off it.</summary>
    public static double DeclineRate(PlayerAttribute attribute) => attribute switch
    {
        // The legs go first, and they go furthest. A pace that settled at seventy is a
        // player the engine can still use; one that settled at five is a name on a list.
        PlayerAttribute.Speed => 0.055,
        PlayerAttribute.Strength => 0.050,
        PlayerAttribute.Stamina => 0.060,
        PlayerAttribute.Reflexes => 0.038,
        PlayerAttribute.Accuracy => 0.030,
        PlayerAttribute.Dribbling => 0.028,
        PlayerAttribute.Heading => 0.026,
        _ => 0.026
    };

    /// <summary>
    /// Where this attribute settles. The floors are as much a design statement as the
    /// rates: a heading that decays to nothing is a rule about a game nobody plays, and a
    /// keeper's reflexes that decay like a striker's pace is a rule about a career that
    /// does not exist.
    /// </summary>
    public static double DeclineFloor(PlayerAttribute attribute) => attribute switch
    {
        PlayerAttribute.Speed => 32,
        PlayerAttribute.Strength => 34,
        PlayerAttribute.Stamina => 30,
        PlayerAttribute.Reflexes => 52,
        PlayerAttribute.Accuracy => 55,
        PlayerAttribute.Dribbling => 55,
        PlayerAttribute.Heading => 55,
        _ => 55
    };

    /// <summary>
    /// How much of a full year's growth a man of this age has left before his peak: all of
    /// it at <see cref="YoungestAge"/>, none of it on the peak itself.
    ///
    /// <para>
    /// It is the difference between a growth curve and a schedule. A schedule gives a
    /// sixteen-year-old and a twenty-four-year-old the same rate and stops both on the same
    /// day; this gives the first man everything and the second nothing, so the growth lands
    /// where the body's window is rather than where the calendar is.
    /// </para>
    /// </summary>
    public static double Momentum(int age, int peak)
    {
        if (age >= peak)
        {
            return 0.0;
        }

        var window = peak - YoungestAge;

        return window <= 0 ? 0.0 : Math.Clamp((double)(peak - age) / window, 0.0, 1.0);
    }

    /// <summary>
    /// The reading of this man that his potential is a ceiling for: a weighted average of
    /// the attributes his position is made of, on the same 1..100 scale as the attributes
    /// themselves.
    ///
    /// <para>
    /// A keeper is read on his keeping and not on his outfield attributes, which is the
    /// same decision the engine makes in <see cref="AttributeWeights.Keeper"/> and for the
    /// same reason — a goalkeeper with good legs is not a better goalkeeper.
    /// </para>
    /// </summary>
    public static double Overall(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        if (player.Position == Position.GK)
        {
            return (player.Reflexes + player.GoalkeeperPower) / 2.0;
        }

        var weights = AttributeWeights.For(player.Position);

        return (player.Speed * weights.Speed
            + player.Accuracy * weights.Accuracy
            + player.Dribbling * weights.Dribbling
            + player.Heading * weights.Heading
            + player.Strength * weights.Strength) / 1.0;
    }

    /// <summary>
    /// How many points of reading this man has left before his potential. Never negative:
    /// a veteran whose overall has gone past his potential is a man at the end of his
    /// career, not a man with a negative amount of room, and the difference only shows up
    /// in what happens to him next.
    /// </summary>
    public static double Headroom(Player player) =>
        Math.Max(0.0, player.Potential - Overall(player));

    /// <summary>
    /// The weights growth is spent along, together with the sum of their squares.
    ///
    /// <para>
    /// The sum of squares is the part worth arguing about. Growth is handed out in
    /// proportion to weight <em>and</em> divided by it, so that the points the reading
    /// actually gains equal the points the year spent. Without that division a
    /// weight-normalised handout would gain <c>Σw²</c> of what was budgeted — about
    /// twenty-eight per cent of it for a defender — and every player in the world would
    /// arrive at his potential several years later than the number said he would, with no
    /// error anywhere to point at.
    /// </para>
    /// </summary>
    public static IReadOnlyList<(PlayerAttribute Attribute, double Weight)> GrowthWeights(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        if (player.Position == Position.GK)
        {
            return new[]
            {
                (PlayerAttribute.Reflexes, 0.5),
                (PlayerAttribute.GoalkeeperPower, 0.5)
            };
        }

        var weights = AttributeWeights.For(player.Position);

        return new[]
        {
            (PlayerAttribute.Speed, weights.Speed),
            (PlayerAttribute.Accuracy, weights.Accuracy),
            (PlayerAttribute.Dribbling, weights.Dribbling),
            (PlayerAttribute.Heading, weights.Heading),
            (PlayerAttribute.Strength, weights.Strength)
        };
    }

    /// <summary>
    /// A year of a man's career: what has passed his peak falls away, and what has not grows
    /// towards his potential by as much as his years of remaining momentum allow.
    ///
    /// <para>
    /// The decline is applied first and the growth is measured against the reading it left,
    /// rather than the reading the year started with. A man crossing his peak in this year
    /// therefore loses this year and gains none, which is the year a career turns over, and
    /// computing the growth off the pre-decline number would have given him a growth year
    /// on the strength of a peak he had just stopped having.
    /// </para>
    /// </summary>
    public static void Develop(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        var age = player.Age;

        foreach (var attribute in All)
        {
            ApplyDecline(player, attribute, age);
        }

        var weights = GrowthWeights(player);
        var headroom = Headroom(player);

        if (headroom > 0.0)
        {
            var momentum = WeightedMomentum(age, weights);
            var budget = headroom * momentum * YearRate;

            if (budget > 0.0)
            {
                SpendOnGrowth(player, weights, budget, headroom);
            }
        }

        ApplyStaminaGrowth(player, age);
    }

    /// <summary>
    /// The tank fills on its own curve and is not part of the reading, so it does not share
    /// the growth budget.
    ///
    /// <para>
    /// It is a body fact and the budget is a rating fact, and mixing them would have meant a
    /// man with a big tank growing slower at everything — which would have made stamina an
    /// eighth rating wearing a body's clothes, and the engine does not read it as one.
    /// </para>
    /// </summary>
    private static void ApplyStaminaGrowth(Player player, int age)
    {
        var momentum = Momentum(age, PeakAge(PlayerAttribute.Stamina));

        if (momentum <= 0.0)
        {
            return;
        }

        var rest = Math.Max(0.0, StaminaCeiling - player.Stamina);

        if (rest <= 0.0)
        {
            return;
        }

        player.Raise(PlayerAttribute.Stamina, rest * momentum * StaminaYearRate);
    }

    private static void ApplyDecline(Player player, PlayerAttribute attribute, int age)
    {
        var peak = PeakAge(attribute);

        if (age <= peak)
        {
            return;
        }

        var floor = DeclineFloor(attribute);
        var value = player.Get(attribute);

        if (value <= floor)
        {
            return;
        }

        var yearsPast = age - peak;
        var acceleration = 1.0 + yearsPast * DeclineAcceleration;

        player.Set(attribute, value - DeclineRate(attribute) * (value - floor) * acceleration);
    }

    /// <summary>
    /// Hands this year's budget out, and refuses to hand out more than the man has.
    ///
    /// <para>
    /// The cap is the reason potential is a real ceiling rather than an aspiration. Each
    /// attribute may take at most its own share of the remaining room, so the reading cannot
    /// cross the potential no matter how the rounding lands — and without it a man whose
    /// weighted attributes hit the top of the scale one at a time would sail past a
    /// potential of seventy-eight and end up a hundred-rated player who was supposed to be
    /// capped.
    /// </para>
    /// </summary>
    private static void SpendOnGrowth(
        Player player,
        IReadOnlyList<(PlayerAttribute Attribute, double Weight)> weights,
        double budget,
        double headroom)
    {
        var sumOfSquares = weights.Sum(weight => weight.Weight * weight.Weight);

        if (sumOfSquares <= 0.0)
        {
            return;
        }

        foreach (var (attribute, weight) in weights)
        {
            var share = budget * weight / sumOfSquares;
            var ceiling = headroom * weight;

            player.Raise(attribute, Math.Min(share, ceiling));
        }
    }

    private static double WeightedMomentum(
        int age,
        IReadOnlyList<(PlayerAttribute Attribute, double Weight)> weights)
    {
        var total = weights.Sum(weight => weight.Weight);

        if (total <= 0.0)
        {
            return 0.0;
        }

        return weights.Sum(weight => Momentum(age, PeakAge(weight.Attribute)) * weight.Weight) / total;
    }

    // --- Potential ----------------------------------------------------------------
    //
    // A new man's potential is not a number the world picks out of a hat. It is his current
    // reading plus a share of what is left, and what is left is a function of his age, so
    // the same draw gives a sixteen-year-old a ceiling of a hundred and a thirty-year-old a
    // ceiling close to where he already is. A potential assigned independently of the man
    // would have had a thirty-five-year-old prospect on the market.

    /// <summary>The reading a man of this age could conceivably reach, at the very top.</summary>
    public static int CeilingFor(int age) => age switch
    {
        <= 19 => 100,
        <= 23 => 97,
        <= 27 => 93,
        <= 30 => 88,
        <= 33 => 82,
        <= 36 => 74,
        <= 39 => 65,
        <= 42 => 55,
        <= 46 => 44,
        <= 50 => 33,
        _ => 20
    };

    /// <summary>
    /// The potential of a newly drawn man, from how good he is now, how old he is, and a
    /// draw in <paramref name="ambition"/> on 0..1.
    ///
    /// <para>
    /// The draw is raised to a power above one, which biases it low: most men land in the
    /// middle of the room between here and where their age says they could get to, and a few
    /// are near the top. A flat draw would hand every man in the world the same expectation,
    /// and a squad where every man has the same room left is a squad with no spread in it —
    /// which is the thing a manager reads a table of stars to find out about.
    /// </para>
    /// </summary>
    public static int PotentialFor(double currentReading, int age, double ambition)
    {
        var ceiling = CeilingFor(age);
        var draw = Math.Clamp(ambition, 0.0, 1.0);
        var shaped = Math.Pow(draw, 1.8);

        // A share of the room between here and where his age says he could get to. The lower
        // bound of a fifth of the way keeps a man who is already good from being handed a
        // potential below himself, which is the one value that would freeze him.
        var share = 0.2 + shaped * 0.8;

        // A man who is already better than the top his age allows is a man the world expects
        // to have finished, so his ceiling is where he is. Ordering the two bounds is the
        // whole of it: Math.Clamp throws when the minimum is above the maximum, and an
        // outstanding thirty-eight-year-old — a reading above CeilingFor(38) — is a perfectly
        // ordinary row for the seeder to draw, so the unguarded form crashed the world on one
        // of them rather than on a rule anybody had argued about.
        var top = Math.Max(ceiling, currentReading);
        var bottom = Math.Min(currentReading, top);
        var value = bottom + (top - bottom) * share;

        return (int)Math.Round(Math.Clamp(value, bottom, top), MidpointRounding.AwayFromZero);
    }
}
