using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Application.Models;

/// <summary>
/// Everything a screen says about <em>where</em> a match is being played: the season and the
/// day, the competition and its phase, the ground, and — for a return leg — the result of the
/// leg that came before it.
///
/// The match screen used to print a fixed line, "DIVISÃO 3 • JOGO-TREINO", above the score of
/// every match in the pyramid. A header that cannot be wrong because it says nothing is a
/// header that tells a manager he is watching a training game in a division he does not play
/// in, and the information it should be carrying is the information a manager reads first: a
/// Supercup is one match, a cup return leg is decided by an aggregate, and a championship
/// matchday is twenty-two games played at once.
/// </summary>
public class MatchContextView
{
    /// <summary>"Temporada II" — the season the match belongs to.</summary>
    public string SeasonName { get; init; } = string.Empty;

    /// <summary>The day of the season's calendar this match is played on.</summary>
    public int MatchDayNumber { get; init; }

    /// <summary>"Copa do Brasil", "Campeonato Brasileiro", "Supercopa Nacional".</summary>
    public string CompetitionName { get; init; } = string.Empty;

    /// <summary>
    /// The edition this match belongs to, and the round of it.
    ///
    /// <para>
    /// They are here because this is the one reader that already had all of them in local
    /// variables — the window knows the edition, the window knows its own number, and the
    /// rounds of the edition are one more read of a table this service has open. A caller
    /// that wanted to say "the tenth place went to the seventh" was having to go and
    /// re-derive the whole chain of ids to find out which table to look at, which is the
    /// shape of work that ends with two screens disagreeing about a table.
    /// </para>
    /// </summary>
    public Guid CompetitionSeasonId { get; init; }

    /// <summary>The round this match was, counted from one inside its own competition.</summary>
    public int RoundNumber { get; init; }

    /// <summary>How many rounds the edition has, which is what "rounds remaining" is read against.</summary>
    public int TotalRounds { get; init; }

    /// <summary>1 is the top of the pyramid, null for a competition that is not a division.</summary>
    public int? Tier { get; init; }

    /// <summary>
    /// Whether a two-legged tie was decided by this match, and by whom.
    ///
    /// A cup tie is over when its window closes, so a report written one tick after the
    /// whistle cannot yet say the club is through — and saying it anyway would be telling a
    /// manager he is in the next round before the rules have said so.
    /// </summary>
    public bool TieResolved { get; init; }

    public Guid? TieWinnerTeamId { get; init; }

    public CompetitionType CompetitionType { get; init; }

    /// <summary>
    /// "Campeonato 1ª Divisão", or the cup's own name. The edition is what a fixture belongs
    /// to and a competition runs three of them in a season, so the phase line says which.
    /// </summary>
    public string EditionName { get; init; } = string.Empty;

    /// <summary>
    /// The phase of the competition, in the words a manager would use: "Rodada 4" in a
    /// championship, "quartas de final" in a cup, and the Supercup's own name.
    /// </summary>
    public string PhaseName { get; init; } = string.Empty;

    /// <summary>
    /// "partida de ida" or "partida de volta" for the two legs of a cup tie, and nothing at
    /// all for anything that is not one. The aggregate is what makes a second leg different
    /// from a first one, so the label travels with the result below the score.
    /// </summary>
    public string? LegLabel { get; init; }

    public string StadiumName { get; init; } = string.Empty;

    public int StadiumCapacity { get; init; }

    /// <summary>
    /// The leg before this one, when there was one. Null for a first leg, for a championship
    /// match and for a Supercup, because those have no previous match to be the answer to.
    /// </summary>
    public CupLegResult? FirstLeg { get; init; }
}

/// <summary>
/// One leg of a cup tie as a screen shows it under a scoreboard: who played, and by how much.
/// </summary>
public class CupLegResult
{
    public Guid HomeTeamId { get; init; }
    public string HomeTeamName { get; init; } = string.Empty;
    public int HomeGoals { get; init; }
    public Guid AwayTeamId { get; init; }
    public string AwayTeamName { get; init; } = string.Empty;
    public int AwayGoals { get; init; }
}
