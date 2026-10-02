using NinjaEleven.Domain.Finance;

namespace NinjaEleven.Domain.Competitions;

/// <summary>
/// One band of a division's table, and what the season's end does to the clubs in it.
/// </summary>
/// <param name="Kind">Whether the band is the title, an access, a fall, or nothing at all.</param>
/// <param name="FromPosition">The first position of the band, counted from one.</param>
/// <param name="ToPosition">The last position of the band.</param>
/// <param name="ToTier">
/// The tier the clubs in the band start the next season in — the tier above an access, the one
/// below a fall, and the tier itself for a club that stays.
/// </param>
/// <param name="ToDivisionName">The division of <paramref name="ToTier"/>, in the game's own words.</param>
/// <param name="Label">The band's name, as a manager would say it.</param>
/// <param name="Meaning">What finishing there is worth.</param>
public sealed record DivisionBand(
    DivisionBandKind Kind,
    int FromPosition,
    int ToPosition,
    int ToTier,
    string ToDivisionName,
    string Label,
    string Meaning);

/// <summary>What a band of a table is for.</summary>
public enum DivisionBandKind
{
    /// <summary>Nothing: a place in the middle of the table.</summary>
    Safe = 0,

    /// <summary>The title, which only the first division's first place can be.</summary>
    Title = 1,

    /// <summary>The places that go up a division.</summary>
    Promotion = 2,

    /// <summary>The places that go down a division.</summary>
    Relegation = 3
}

/// <summary>
/// One division of the pyramid, and the whole of what the season's end does to its table.
///
/// Everything in it is read from the rules rather than written beside them. The bands come out
/// of <see cref="DivisionMovement.From"/> applied to a table of the right size, which is the
/// same call the close of the season makes, so a band a manager is shown and a club that
/// actually moves cannot be two different things.
/// </summary>
/// <param name="Tier">Which division, counted from one at the top.</param>
/// <param name="Name">The division's own name: "1ª Divisão".</param>
/// <param name="Clubs">How many clubs share its table.</param>
/// <param name="Purse">What the whole table is paid out of.</param>
/// <param name="Bands">The bands, in the order a manager reads them down the table.</param>
public sealed record DivisionRule(
    int Tier,
    string Name,
    int Clubs,
    decimal Purse,
    IReadOnlyList<DivisionBand> Bands);

/// <summary>
/// The pyramid's rules, said the way a manager planning a season needs to read them.
///
/// The screen behind this is a "regras" page, and the reason it exists is that a table of
/// bands without the bands' rules is a table of colours: a club painted for access is told
/// nothing about how many places there are, and a manager who cannot see the chain that
/// orders his table cannot tell whether the club above him is there on points or on a
/// tiebreaker he did not know about.
///
/// So the rules are read from the two places that own them — the chain in
/// <see cref="StandingTable"/> and the movement in <see cref="DivisionMovement.From"/> — and
/// carried out. Nothing here is a second copy: a screen that printed "4 primeiros sobem" from
/// a constant of its own would be a screen that is wrong the day the pyramid is five deep,
/// and the band it painted would still be the one the season painted on the table.
/// </summary>
public static class PyramidRules
{
    /// <summary>How many divisions the pyramid has. Tier 1 is the top.</summary>
    public static int DivisionCount => CompetitionRules.DivisionCount;

    /// <summary>Clubs in every division's table.</summary>
    public static int ClubsPerDivision => CompetitionRules.ClubsPerDivision;

    /// <summary>
    /// The order a table is settled in. It is the sort's own list, so a criterion printed for a
    /// manager is a criterion the game applies.
    /// </summary>
    public static IReadOnlyList<StandingCriterion> TieBreakers => StandingTable.Chain;

    /// <summary>
    /// Every division of the pyramid, from the top, with the bands its table ends the season in.
    /// </summary>
    public static IReadOnlyList<DivisionRule> Divisions() =>
        CompetitionRules.Tiers().Select(Describe).ToList();

    /// <summary>
    /// One division's own rules.
    /// </summary>
    public static DivisionRule Describe(int tier) => new(
        tier,
        CompetitionRules.DivisionName(tier),
        CompetitionRules.ClubsPerDivision,
        PrizeRules.PurseForTier(tier),
        BandsFor(tier));

    /// <summary>
    /// The bands of one division's table, worked out by running the season's own movement over a
    /// table of the right size.
    ///
    /// A synthetic table rather than a formula, and the reason is that the formula is the bug
    /// this avoids: the first division has no division above it and the last has none below, so
    /// a table of bands written as "the top four go up, the bottom four go down" promises the
    /// first division four accesses and the last four falls that have nowhere to go. Asking
    /// <see cref="DivisionMovement.From"/> instead gives a division its own bands, and the
    /// empty ones stay empty.
    /// </summary>
    private static IReadOnlyList<DivisionBand> BandsFor(int tier)
    {
        var clubs = CompetitionRules.ClubsPerDivision;

        var positions = Enumerable.Range(1, clubs)
            .Select(position => new StandingEntry { TeamId = GuidFor(tier, position) })
            .ToList();

        var movement = DivisionMovement.From(tier, positions);
        var bands = new List<DivisionBand>();

        // The title is every division's first place, and not only the first division's: the
        // champion of the third division takes a trophy and the largest share of the third
        // division's purse, and a band that showed it only at the top of the pyramid would be
        // saying that the third division has no champion at all. What the first division's
        // title adds is the season's — the Supercup is contested between it and the cup's
        // winner — and that is said on its own line rather than folded into the band.
        bands.Add(new DivisionBand(
            DivisionBandKind.Title,
            FromPosition: 1,
            ToPosition: 1,
            ToTier: tier,
            CompetitionRules.DivisionName(tier),
            "Campeão da divisão",
            tier == 1
                ? "Troféu, a maior cota da bolsa e a vaga na Supercopa contra o campeão da Copa, na abertura da próxima temporada."
                : "Troféu, a maior cota da bolsa e a classificação para a divisão de cima."));

        bands.AddRange(BandOf(movement, DivisionBandKind.Promotion));
        bands.AddRange(BandOf(movement, DivisionBandKind.Relegation));
        bands.AddRange(StayedIn(movement));

        return bands.OrderBy(band => band.FromPosition).ToList();
    }

    /// <summary>
    /// The band of clubs that finish where they are, which is the question asked at the middle
    /// of a table and the one a set of movement rules never answers: they say where four clubs
    /// go and where four clubs fall, and the eight in between are left to the manager to guess.
    /// </summary>
    private static IEnumerable<DivisionBand> StayedIn(DivisionMovement movement)
    {
        // The champion is not in this band even when it stays — which is only ever the first
        // division, where the title is the whole of what first place is. A club that finished
        // first and stayed is a champion, and saying it stayed would put it in two bands.
        var positions = movement.Movements
            .Where(entry => entry.Stays && entry.Position > 1)
            .Select(entry => entry.Position)
            .OrderBy(position => position)
            .ToList();

        if (positions.Count == 0) return [];

        return
        [
            new DivisionBand(
                DivisionBandKind.Safe,
                FromPosition: positions[0],
                ToPosition: positions[^1],
                movement.FromTier,
                CompetitionRules.DivisionName(movement.FromTier),
                "Permanece na divisão",
                "A tabela termina e o clube joga a próxima temporada onde terminou. A bolsa é paga mesmo assim.")
        ];
    }

    /// <summary>
    /// One contiguous band of the clubs that all move the same way, or nothing when no club in
    /// the division moves that way at all.
    /// </summary>
    private static IEnumerable<DivisionBand> BandOf(DivisionMovement movement, DivisionBandKind kind)
    {
        var wanted = kind == DivisionBandKind.Promotion
            ? movement.Promoted
            : movement.Relegated;

        if (wanted.Count == 0) return [];

        var positions = movement.Movements
            .Where(movementEntry => wanted.Contains(movementEntry.TeamId))
            .Select(movementEntry => movementEntry.Position)
            .OrderBy(position => position)
            .ToList();

        var toTier = kind == DivisionBandKind.Promotion
            ? movement.FromTier - 1
            : movement.FromTier + 1;

        return
        [
            new DivisionBand(
                kind,
                FromPosition: positions[0],
                ToPosition: positions[^1],
                toTier,
                CompetitionRules.DivisionName(toTier),
                kind == DivisionBandKind.Promotion ? "Zona de acesso" : "Zona de rebaixamento",
                kind == DivisionBandKind.Promotion
                    ? $"{wanted.Count} {(wanted.Count == 1 ? "clube sobe" : "clubes sobem")} para a {CompetitionRules.DivisionName(toTier)} na próxima temporada."
                    : $"{wanted.Count} {(wanted.Count == 1 ? "clube cai" : "clubes caem")} para a {CompetitionRules.DivisionName(toTier)} na próxima temporada.")
        ];
    }

    /// <summary>
    /// A club id for a synthetic line. The pyramid's rules never look at who a club is, only at
    /// where it finished, so these only have to be distinct — and they are, because a division's
    /// sixteen lines are sixteen different places in a table.
    /// </summary>
    private static Guid GuidFor(int tier, int position)
    {
        var bytes = new byte[16];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, 4), tier);
        BitConverter.TryWriteBytes(bytes.AsSpan(4, 4), position);
        return new Guid(bytes);
    }
}
