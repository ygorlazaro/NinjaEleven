namespace NinjaEleven.Application.Models;

/// <summary>
/// Where a club stands, and in which division: the two questions a club's own page asks about
/// the season it is in.
///
/// It is asked of a club and not of an edition because a club is what a manager opens. A club
/// has no division of its own — it is enrolled in one for a season — so the edition comes back
/// with the answer, and a screen can say "4ª Divisão, 7º" without having to work out which of
/// the season's editions the club is in, or read four tables to find the one line it wanted.
/// </summary>
public class ClubStanding
{
    public required Guid TeamId { get; init; }
    public required Guid SeasonId { get; init; }

    /// <summary>
    /// The edition the club is in, and null when it is in no division this season. A club that
    /// has not been enrolled is a fact about the world rather than an error, and it is said
    /// with a null instead of an exception so a page can show the club without a table beside it.
    /// </summary>
    public Guid? CompetitionSeasonId { get; init; }

    /// <summary>"4ª Divisão", or empty when the club is in none.</summary>
    public string DivisionName { get; init; } = string.Empty;

    /// <summary>1 is the top of the pyramid. Null when the club is in no division.</summary>
    public int? Tier { get; init; }

    /// <summary>
    /// The strength of the club's squad this season, as the table's own seed is measured: the
    /// average of every man on the books, half a star at a time.
    ///
    /// It travels with the division rather than being a second answer from somewhere else,
    /// because a club's strength is a fact about its squad and its season and not about the
    /// division it happens to be in — a club in no division of this season still has one.
    /// </summary>
    public double SquadStars { get; init; }

    /// <summary>
    /// How many clubs the division holds, so a position can be said as one of a number: "7º de
    /// 12" is a place in a table, and "7º" alone is a place in a list the reader cannot see.
    /// It is the size of the table the club is on, and it is the backend's because the pyramid
    /// is a rule rather than a number a screen may keep to itself.
    /// </summary>
    public int? ClubsInDivision { get; init; }

    /// <summary>
    /// The club's own line, or null when the division has not been drawn yet. The numbers on it
    /// are the same ones the division's table carries, because it is that table's line.
    /// </summary>
    public StandingRow? Row { get; init; }
}
