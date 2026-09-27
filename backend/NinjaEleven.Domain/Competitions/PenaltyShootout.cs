using NinjaEleven.Domain.Common;

namespace NinjaEleven.Domain.Competitions;

/// <summary>
/// A penalty shootout: five kicks each, then sudden death, until one club has scored one more
/// than the other.
///
/// It is a domain rule of the cup and not a half of a match. The two legs of a tie are
/// ordinary matches that end at ninety minutes, and the shootout happens the moment the
/// second one is over — it is not something the match engine runs the clock through, and
/// calling it a match half would mean a cup tie that took two and a half hours of the
/// manager's evening to settle. It is drawn from the tie's own seed, so a replayed tie takes
/// the same penalties in the same order.
/// </summary>
public sealed class PenaltyShootout
{
    /// <summary>Kicks each club takes before sudden death starts.</summary>
    public const int KicksPerSide = 5;

    /// <summary>
    /// A cap on sudden death pairs. Two sides that both score every kick are a measure-zero
    /// event, but a loop that can only end by chance still gets a floor under it so a
    /// pathological random source cannot keep a season from finishing.
    /// </summary>
    private const int MaxSuddenDeathPairs = 50;

    private PenaltyShootout() { }

    public int HomeGoals { get; private init; }
    public int AwayGoals { get; private init; }
    public Guid HomeTeamId { get; private init; }
    public Guid AwayTeamId { get; private init; }
    public Guid WinnerTeamId { get; private init; }
    public bool IsSuddenDeath { get; private init; }

    /// <summary>
    /// A shootout never ends level. The property exists so a caller holding an unrun shootout
    /// cannot be mistaken for one that finished 4-4 after four rounds.
    /// </summary>
    public bool IsDraw => WinnerTeamId == Guid.Empty;

    /// <summary>
    /// Runs the kicks, in the order everybody has seen them: home, away, home, away.
    ///
    /// The taker's accuracy decides each one, so the eleven that played the tie decide it: a
    /// club whose forwards cannot finish does not win a shootout by luck any more than it
    /// wins a match by luck.
    /// </summary>
    /// <param name="homeTeamId">The club that was at home in the first leg.</param>
    /// <param name="awayTeamId">The club that was at home in the second leg.</param>
    /// <param name="homeConversion">Chance a home taker scores, 0..1.</param>
    /// <param name="awayConversion">Chance an away taker scores, 0..1.</param>
    public static PenaltyShootout Simulate(
        Guid homeTeamId,
        Guid awayTeamId,
        double homeConversion,
        double awayConversion,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (homeTeamId == awayTeamId)
        {
            throw new ArgumentException("A shootout needs two clubs.", nameof(homeTeamId));
        }

        var homeScored = 0;
        var awayScored = 0;
        var pair = 0;

        while (true)
        {
            pair++;

            if (random.NextDouble() < homeConversion) homeScored++;
            if (random.NextDouble() < awayConversion) awayScored++;

            if (pair <= KicksPerSide)
            {
                // Still in the regular five. The shootout is over the moment one club is
                // further ahead than the other has kicks left to close: a side two up after
                // three kicks is out, and making it take two more would be theatre.
                var kicksLeft = KicksPerSide - pair;

                if (homeScored > awayScored && homeScored > awayScored + kicksLeft) break;
                if (awayScored > homeScored && awayScored > homeScored + kicksLeft) break;

                continue;
            }

            // Sudden death: one kick each and the first side ahead after the pair is through.
            if (homeScored != awayScored) break;
            if (pair >= KicksPerSide + MaxSuddenDeathPairs) break;
        }

        return new PenaltyShootout
        {
            HomeGoals = homeScored,
            AwayGoals = awayScored,
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            WinnerTeamId = homeScored > awayScored
                ? homeTeamId
                : awayScored > homeScored
                    ? awayTeamId
                    : Guid.Empty,
            IsSuddenDeath = pair > KicksPerSide
        };
    }
}
