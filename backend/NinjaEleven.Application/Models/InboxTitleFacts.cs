namespace NinjaEleven.Application.Models;

/// <summary>
/// A cup, a championship or an artilharia the club has won, in the words a trophy is
/// announced in.
///
/// The three are told apart because a manager reads them differently: a title is the season,
/// a cup is a run, and an artilharia is one man's afternoon — and a single " parabéns" sent
/// for all three would be the one message in the box that says nothing at all.
/// </summary>
public class TitleFacts
{
    public required Guid RecipientTeamId { get; init; }
    public required string ClubName { get; init; }

    public required InboxTitleKind Kind { get; init; }

    /// <summary>"1ª Divisão", "Copa Ninja Eleven" — the competition the prize came from.</summary>
    public required string CompetitionName { get; init; }

    /// <summary>The season the prize belongs to, as a manager would say it.</summary>
    public string? SeasonName { get; init; }

    /// <summary>
    /// The place the club took, when a place is what was won. A champion is first by
    /// definition, a cup winner is first of one tie, and an artilharia is a table of its own
    /// where the winner can be second and still be the winner.
    /// </summary>
    public int? Position { get; init; }

    public decimal? PrizeMoney { get; init; }

    /// <summary>
    /// The man the artilharia belongs to, when the prize is a man's. It is carried with the
    /// club's name because the two travel together in the headline: the artilharia is the
    /// club's money and the striker's afternoon, and a message that said only one of them
    /// would make the other look like an accident.
    /// </summary>
    public Guid? PlayerId { get; init; }

    public string? PlayerName { get; init; }

    public int? PlayerGoals { get; init; }

    /// <summary>
    /// Where the manager should be sent to see the prize, when the kind of title does not
    /// already say. The cup's artilharia lives on the cup's screen and not on the
    /// championship's, and a message that sent a manager to the wrong table to read the
    /// table he had just won would be the only link in the game that lied.
    /// </summary>
    public string? LinkRoute { get; init; }

    public string? LinkLabel { get; init; }

    /// <summary>
    /// What this prize is the announcement of, and the guard against announcing it twice.
    /// It is the reference the money itself was paid under, so a prize that is announced is
    /// a prize that was paid.
    /// </summary>
    public string Reference { get; init; } = string.Empty;
}

/// <summary>Which of the three things a club can be the best at.</summary>
public enum InboxTitleKind
{
    Championship,
    Cup,
    Scorer
}
