namespace NinjaEleven.Domain.Competitions;

/// <summary>
/// One tie-round of the cup: how many clubs are still in it, how many ties that is, and the two
/// days it is played over.
/// </summary>
/// <param name="TieRound">Which tie-round this is, counted from one at the 32-avos.</param>
/// <param name="Name">The round's name, in the game's own words: "quartas de final".</param>
/// <param name="ClubsIn">How many clubs enter the round.</param>
/// <param name="Ties">How many ties the round is made of, and therefore how many clubs leave it.</param>
/// <param name="FirstLegDay">The day of the season the first legs are played on.</param>
/// <param name="SecondLegDay">The day of the season the returns are played on.</param>
public sealed record CupRoundRule(
    int TieRound,
    string Name,
    int ClubsIn,
    int Ties,
    int FirstLegDay,
    int SecondLegDay);

/// <summary>
/// The cup's rules, said the way a manager planning his run needs to read them.
///
/// This is the knockout's answer to what <c>PyramidRules</c> is to the championship, and it exists
/// for the same reason. The bracket shows a manager which two clubs are in a tie and the prize
/// legend shows what the run is worth, and between those two there is nothing telling him that a
/// tie is two matches, that the aggregate is what decides it, or that a level aggregate goes
/// straight to penalties with no extra time in between. Those are the three facts a manager
/// planning a season needs and the three a bracket taken on its own cannot say.
///
/// Every number here is read from <see cref="CompetitionRules"/> rather than written beside it, so
/// a screen printing "são jogos de ida e volta" from a constant of its own is a screen that is
/// wrong the day the cup stops being two-legged, and the bracket it is explaining would still be
/// the bracket the game drew.
/// </summary>
public static class CupRules
{
    /// <summary>How many clubs the cup is drawn from — the whole world, all sixty-four of them.</summary>
    public static int Size => CompetitionRules.CupSize;

    /// <summary>How many tie-rounds the cup is drawn over.</summary>
    public static int TieRounds => CompetitionRules.CupRounds;

    /// <summary>Matches in a cup tie, and the reason a tie needs two matchdays.</summary>
    public static int LegsPerTie => CompetitionRules.CupTieLegs;

    /// <summary>
    /// Whether a cup tie can go to extra time.
    ///
    /// It cannot, and it is said as a flag rather than left out because its absence is the point:
    /// a manager planning a tie he expects to be level needs to know that the game will not give
    /// anybody thirty more minutes. A cup tie that is level on the aggregate is decided by
    /// penalties, which is what <see cref="CupTie.Resolve"/> enforces — it refuses a level
    /// aggregate with no shootout rather than picking a winner.
    /// </summary>
    public static bool AllowsExtraTime => false;

    /// <summary>
    /// What decides a tie that is level on the aggregate, in the words the screen prints.
    ///
    /// The sentence travels from the domain for the same reason a standing criterion's does: it
    /// is a rule, and a rule restated on a client is a second copy of the competition that nobody
    /// updates.
    /// </summary>
    public static string LevelTieRule =>
        $"Um empate no agregado vai direto para os pênaltis — não há prorrogação. "
        + $"Os pênaltis são cobrados do mesmo time que jogou as duas pernas, e o vencedor da "
        + $"disputa é quem passa.";

    /// <summary>
    /// The legs of a tie, and how the two of them add up.
    ///
    /// The aggregate is what decides a tie and not the score of the second leg, and saying so is
    /// the whole reason a manager who loses away from home can still be in the cup: the two legs
    /// swap ends, so a club that wins the return by two can go out on the aggregate.
    /// </summary>
    public static string AggregateRule =>
        $"Cada confronto é jogado em {LegsPerTie} jogos, e quem passa é quem somar mais gols nos "
        + $"dois — o agregado. A segunda partida não vale por si só: um clube que perde em casa "
        + $"por um gol e vence fora por dois ainda passa.";

    /// <summary>
    /// What the cup's winner goes on to play, which is the one thing a knockout decides about the
    /// season after it.
    /// </summary>
    public static string WinnerTakesRule =>
        "O campeão da Copa se classifica para a Supercopa da próxima temporada, contra o campeão "
        + "da 1ª Divisão.";

    /// <summary>
    /// Every tie-round, from the 32-avos to the final, with the clubs in it and the days it is
    /// played over.
    ///
    /// The club counts are halved off <see cref="CompetitionRules.CupSize"/> rather than written
    /// out, because sixty-four halving six times is the final being two clubs and not a screen's
    /// arithmetic: a cup drawn over eight rounds would print a final of two clubs and seven rows
    /// of numbers that do not divide.
    /// </summary>
    public static IReadOnlyList<CupRoundRule> Rounds()
    {
        var rounds = new List<CupRoundRule>(CompetitionRules.CupRounds);
        var clubs = CompetitionRules.CupSize;

        for (var tieRound = 1; tieRound <= CompetitionRules.CupRounds; tieRound++)
        {
            var (firstLeg, secondLeg) = CompetitionRules.CupLegMatchDays(tieRound);

            rounds.Add(new CupRoundRule(
                tieRound,
                CompetitionRules.TieRoundName(tieRound),
                clubs,
                clubs / 2,
                firstLeg,
                secondLeg));

            clubs /= 2;
        }

        return rounds;
    }
}
