namespace NinjaEleven.Domain.Teams;

/// <summary>
/// One club's memory of another club: the games they have played and what they were like.
/// </summary>
/// <remarks>
/// This is a fact about a <em>pair</em>, so it is not a column on either club. "Grêmio's
/// rivalry with Internacional" belongs to both of them and to neither of them, and a column on
/// the club would have to be written twice and could be read twice with two different answers.
/// It is a row keyed by the ordered pair, which is what makes a rivalry reciprocal by
/// construction rather than by two writers agreeing.
/// </remarks>
public readonly record struct RivalryFixture(
    DateTimeOffset PlayedAt,
    bool WasHome,
    int GoalsFor,
    int GoalsAgainst);

/// <summary>
/// How much a rivalry has grown over one season, and why.
/// </summary>
/// <remarks>
/// The bands are kept apart so a screen can say <em>what</em> made a rivalry what it is. A
/// manager who has just been beaten four times by one club is owed the sentence "close
/// matches" and not the sentence "you played often", because those are different problems and
/// only one of them is about form.
/// </remarks>
public readonly record struct RivalryGrowth(
    double Recurrence,
    double Decisiveness,
    double Results,
    double Streaks,
    double RecentForm,
    double Overall);

/// <summary>
/// What a rivalry is made of, and how it grows.
///
/// <para>
/// A rivalry is a number, and the number is a weighted reading of five things. The weights are
/// here, in one place, because a rivalry worked out one way on a screen and another way in the
/// engine is a rivalry whose cause changes when you look at it from the other side.
/// </para>
///
/// <para>
/// <b>Recurrence is the heaviest thing and frequency is not a criterion at all.</b> That is not
/// an oversight. A division's fixture list gives every pair of clubs exactly two meetings a
/// season, so frequency is the same number for all two hundred and forty pairs a manager could
/// be rivals with and it cannot separate any of them. It was given a weight of zero for the
/// same reason a constant is given a band of zero in the balance laboratory: a criterion that
/// cannot tell two things apart is not carrying information, and giving it a share of the
/// answer is a way of making the other four share less.
/// </para>
///
/// <para>
/// <b>Decisiveness is a margin, not a score.</b> One nil and one four-nil are both a win on the
/// scoreline, and only one of them is a match anybody in the stand would call a rivalry. The
/// band rewards the narrow games and never quite reaches zero, because a rivalry decided by six
/// goals is still a rivalry that happened.
/// </para>
/// </summary>
public static class RivalryRules
{
    /// <summary>How many rivals a club may name. Four, and four is a number a screen can print.</summary>
    public const int MaxRivals = 4;

    /// <summary>
    /// How much of a rivalry is how often the two clubs have met.
    /// </summary>
    public const double RecurrenceWeight = 0.35;

    /// <summary>How much of it is how close the games were.</summary>
    public const double DecisivenessWeight = 0.25;

    /// <summary>How much of it is who has been winning.</summary>
    public const double ResultsWeight = 0.20;

    /// <summary>How much of it is runs of games without a break.</summary>
    public const double StreaksWeight = 0.12;

    /// <summary>How much of it is what has been happening lately.</summary>
    public const double RecentFormWeight = 0.08;

    /// <summary>How many of the last meetings count as "lately".</summary>
    public const int RecentGames = 6;

    /// <summary>Games that are decided by this many goals or more are not a rivalry.</summary>
    public const int DecisiveMargin = 4;

    /// <summary>The top of the scale. A rivalry can reach this and it is the end of the scale.</summary>
    public const double Ceiling = 1.0;

    /// <summary>
    /// The floor below which an established rivalry never falls.
    /// </summary>
    /// <remarks>
    /// A club that does not play another club this season has not stopped being its rival. The
    /// floor is what says so, and without it a promoted club would lose every rivalry it had the
    /// season it was in a different division — which is the one season a rivalry should be able
    /// to sit out.
    /// </remarks>
    public const double Floor = 0.05;

    /// <summary>
    /// How close two clubs are to each other on the scale.
    /// </summary>
    public static double Recurrence(IReadOnlyList<RivalryFixture> fixtures)
    {
        if (fixtures.Count == 0)
        {
            return 0;
        }

        // A pair that has met a lot is near the ceiling long before a pair that has met twice.
        var meetings = (double)fixtures.Count;
        var ratio = meetings / (meetings + RecurrenceSaturation);

        return Math.Clamp(ratio / SaturationCeiling, 0, 1);
    }

    /// <summary>
    /// Where <see cref="Recurrence"/> reaches its ceiling: four meetings a season is as much as a
    /// club can be made to play one side, because a pair has two.
    /// </summary>
    public const int RecurrenceSaturation = 4;

    private const double SaturationCeiling = 0.8;

    /// <summary>
    /// How narrow the games have been.
    /// </summary>
    /// <remarks>
    /// A goalless draw and a one-nil are both close; a four-nil is not. The band is
    /// <c>1 - margin / DecisiveMargin</c>, so the measure is the gap in goals and the scale is
    /// the width of a match that anybody would call a rivalry.
    /// </remarks>
    public static double Decisiveness(IReadOnlyList<RivalryFixture> fixtures)
    {
        if (fixtures.Count == 0)
        {
            return 0;
        }

        var total = fixtures.Sum(fixture => (double)Closeness(fixture.GoalsFor, fixture.GoalsAgainst));

        return Math.Clamp(total / fixtures.Count, 0, 1);
    }

    private static double Closeness(int goalsFor, int goalsAgainst)
    {
        var margin = Math.Abs(goalsFor - goalsAgainst);

        return margin >= DecisiveMargin
            ? 0
            : 1.0 - margin / (double)DecisiveMargin;
    }

    /// <summary>
    /// How level the pair has been, as opposed to how one-sided it is.
    /// </summary>
    /// <remarks>
    /// This is deliberately not "who won more", because a rivalry that one side wins every
    /// time is a mismatch and not a rivalry. A pair split down the middle scores the full
    /// mark whatever the actual results were — three wins, three defeats is the most evenly
    /// contested thing two clubs can do to each other.
    /// </remarks>
    public static double Results(IReadOnlyList<RivalryFixture> fixtures)
    {
        if (fixtures.Count == 0)
        {
            return 0;
        }

        var points = fixtures.Sum(fixture => PointsOf(fixture.GoalsFor, fixture.GoalsAgainst));
        var possible = fixtures.Sum(_ => 3d);

        if (possible <= 0)
        {
            return 0;
        }

        // One club taking every point gives zero; the pair splitting them gives one. A win rate
        // rather than a goal difference, because a rivalry is about who has been beating whom.
        var share = points / possible;
        var evenness = 1 - Math.Abs(share - 0.5) / 0.5;

        // A pair of clubs drawing every game has met a lot and decided nothing, which is the
        // one case where evenness alone would call it the greatest rivalry in the country. The
        // decisiveness band is the half of the answer that says a draw is worth something.
        return Math.Clamp(evenness * (0.5 + 0.5 * Decisiveness(fixtures)), 0, 1);
    }

    private static double PointsOf(int goalsFor, int goalsAgainst) =>
        goalsFor > goalsAgainst ? 3
        : goalsFor == goalsAgainst ? 1
        : 0;

    /// <summary>
    /// The longest run of games won in a row, and the longest run not won, over the pair.
    /// </summary>
    /// <remarks>
    /// Both ends count, because a rivalry the other side is on is still a rivalry. A pair that
    /// trades one-nil wins and one-nil defeats twenty times over has no long run either way and
    /// would score nothing here — which is right: it is a fixture, not a story.
    /// </remarks>
    public static double Streaks(IReadOnlyList<RivalryFixture> fixtures)
    {
        if (fixtures.Count == 0)
        {
            return 0;
        }

        var longestWin = 1;
        var longestLoss = 1;
        var win = 1;
        var loss = 1;

        for (var index = 1; index < fixtures.Count; index++)
        {
            var fixture = fixtures[index];
            var previous = fixtures[index - 1];

            if (Won(fixture) == Won(previous))
            {
                if (Won(fixture))
                {
                    win++;
                    longestWin = Math.Max(longestWin, win);
                }
                else
                {
                    loss++;
                    longestLoss = Math.Max(longestLoss, loss);
                }
            }
            else
            {
                win = 1;
                loss = 1;
            }
        }

        var longest = Math.Max(longestWin, longestLoss);
        var ratio = (double)(longest - 1) / StreakSaturation;

        return Math.Clamp(ratio, 0, 1);
    }

    /// <summary>How long a run has to be before it is the whole story.</summary>
    public const int StreakSaturation = 4;

    private static bool Won(RivalryFixture fixture) => fixture.GoalsFor > fixture.GoalsAgainst;

    /// <summary>
    /// What has been happening between the two lately, which is the smallest of the five bands
    /// because it is the only one that can change next week.
    /// </summary>
    public static double RecentForm(IReadOnlyList<RivalryFixture> fixtures)
    {
        if (fixtures.Count == 0)
        {
            return 0;
        }

        var recent = fixtures
            .OrderByDescending(fixture => fixture.PlayedAt)
            .Take(RecentGames)
            .ToList();

        var share = recent.Sum(fixture => PointsOf(fixture.GoalsFor, fixture.GoalsAgainst))
            / (recent.Count * 3d);

        return Math.Clamp(share, 0, 1);
    }

    /// <summary>
    /// The rivalry itself, from the meetings it is made of.
    /// </summary>
    /// <remarks>
    /// The five bands are each read once and weighted once, and the total is what the club's
    /// rivals are ordered by. A band that is not part of the answer is not summed in with a
    /// weight of zero either — it is not here at all, which is a different thing and the reason
    /// <see cref="FrequencyWeight"/> does not exist to be set.
    /// </remarks>
    public static double Score(IReadOnlyList<RivalryFixture> fixtures) =>
        fixtures.Count == 0
            ? 0
            : RecurrenceWeight * Recurrence(fixtures)
            + DecisivenessWeight * Decisiveness(fixtures)
            + ResultsWeight * Results(fixtures)
            + StreaksWeight * Streaks(fixtures)
            + RecentFormWeight * RecentForm(fixtures);

    /// <summary>
    /// What a rivalry is worth at the end of a season, given what it was worth before.
    /// </summary>
    /// <remarks>
    /// <b>A rivalry only ever grows, and it never quite dies.</b> That is the whole of the
    /// carry-over, and both halves of it were the wrong way round to begin with. A club that
    /// draws its score from one season's two fixtures instead of from its history would forget a
    /// rivalry built over nine years every time the season turned over, and a manager reading the
    /// screen would watch his oldest grudge be replaced by whoever he happened to play in March.
    ///
    /// <para>
    /// So a season's games are a floor on the number and not a replacement for it: the pair is
    /// worth at least what this season made it worth, and at least as much as it was, and never
    /// less than <see cref="Floor"/> once it has ever been anything. A season with no meetings at
    /// says nothing and changes nothing — which is what a fixture list with no fixture in it means.
    /// </para>
    /// </remarks>
    public static double AfterASeason(double carried, IReadOnlyList<RivalryFixture> season) =>
        Math.Clamp(
            season.Count == 0 ? carried : Math.Max(carried, Score(season)),
            carried > 0 ? Floor : 0,
            Ceiling);
}
