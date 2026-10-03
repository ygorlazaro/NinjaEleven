namespace NinjaEleven.Domain.Teams;

/// <summary>
/// What a seat costs and what that does to the crowd.
///
/// The curve is a table rather than a formula because the table is the rule. It was drawn as
/// points — five limos fills more than a third again as much as thirty — and a curve fitted
/// through them would be a different curve, so the points are the points and everything
/// between them is interpolated. A manager who will one day set his own price needs the
/// demand for any price in between to be an honest reading of the same curve, not a
/// different function that happens to agree at the anchors.
///
/// Below the cheapest price and above the dearest the ends are held flat rather than
/// extrapolated: a free ticket does not fill a ground ten times over, and an absurd price does
/// not empty it beyond saving.
/// </summary>
public static class TicketPriceRules
{
    /// <summary>The price a club opens at, and the price the curve is normalised to 1.00 at.</summary>
    public const decimal ReferencePrice = 10m;

    /// <summary>
    /// Price against demand. Read down the pairs, not across: at ten limos a seat is worth
    /// exactly 1.00 of the crowd, at five it is worth 1.30 of it, and at thirty it is worth
    /// 0.38.
    /// </summary>
    private static readonly (decimal Price, double Demand)[] Curve =
    {
        (5m, 1.30),
        (7m, 1.18),
        (10m, 1.00),
        (12m, 0.93),
        (15m, 0.82),
        (20m, 0.65),
        (25m, 0.50),
        (30m, 0.38)
    };

    /// <summary>
    /// The points the curve is drawn through, cheapest first.
    ///
    /// <para>
    /// They are read rather than restated so that anything which reasons about the shape of the
    /// curve — what a club's seat should cost, how much a price move is worth — works on the
    /// same anchors this file does. A second copy of these numbers would be a second curve, and
    /// it would start agreeing with this one only until somebody retuned this one.
    /// </para>
    /// </summary>
    public static IReadOnlyList<(decimal Price, double Demand)> Anchors => Curve;

    public const double CheapestPriceFactor = 1.30;

    public const double DearestPriceFactor = 0.38;

    /// <summary>How much a seat at a given price is worth to a crowd.</summary>
    public static double DemandFactor(decimal price)
    {
        if (price < Curve[0].Price) return Curve[0].Demand;
        if (price >= Curve[^1].Price) return Curve[^1].Demand;

        for (var index = 1; index < Curve.Length; index++)
        {
            var (highPrice, highDemand) = Curve[index];
            if (price > highPrice) continue;

            var (lowPrice, lowDemand) = Curve[index - 1];
            var span = (double)(highPrice - lowPrice);

            // The ends are different prices by construction, so the span is never zero here.
            var progress = span <= 0 ? 0 : (double)(price - lowPrice) / span;

            return lowDemand + (highDemand - lowDemand) * progress;
        }

        return 1.0;
    }
}
