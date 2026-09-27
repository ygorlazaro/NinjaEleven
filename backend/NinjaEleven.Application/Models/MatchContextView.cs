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
