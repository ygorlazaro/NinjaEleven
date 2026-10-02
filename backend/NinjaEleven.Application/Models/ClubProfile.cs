using NinjaEleven.Domain.Competitions;

namespace NinjaEleven.Application.Models;

/// <summary>
/// The kind of a moment in a club's life, as the shelf of memory names it.
/// </summary>
/// <remarks>
/// The eight kinds split into two families, and the split is the whole design of the history.
///
/// <para>
/// Five of them are <em>derived</em>: a founding, a promotion, a relegation, a title and a
/// season's top scorer are all facts the world already keeps, in `competition_participants`,
/// `trophy_awards` and `match_player_statistics`. Nothing writes them down twice, and the
/// page works them out when it is asked.
/// </para>
///
/// <para>
/// Three of them are <em>recorded</em>, because they are decisions and not results: a rename,
/// a new crest and a change of colours leave nothing in the world to derive them from. Those
/// are rows in <c>club_events</c>, written by the same call that performs the change.
/// </para>
///
/// <para>
/// So this enum is the union of both, and a screen that has never seen a kind still draws a
/// line for it: a manager should be able to read a moment of his club's past that the page did
/// not have a name for, rather than find it missing.
/// </para>
/// </remarks>
public enum ClubHistoryKind
{
    /// <summary>The club's first season in the pyramid, derived from its earliest participation.</summary>
    FirstSeason = 1,

    /// <summary>The club was renamed by the manager running it.</summary>
    NameChange = 2,

    /// <summary>The club's crest was drawn or taken away.</summary>
    CrestChange = 3,

    /// <summary>The club's two colours were changed.</summary>
    ColorsChange = 4,

    /// <summary>One of the club's men topped an edition's scoring chart.</summary>
    TopScorer = 5,

    /// <summary>The club won the edition.</summary>
    Title = 6,

    /// <summary>The club moved up a division between two seasons.</summary>
    Promotion = 7,

    /// <summary>The club moved down a division between two seasons.</summary>
    Relegation = 8
}

/// <summary>
/// One moment in a club's life, as the page reads it.
/// </summary>
public class ClubHistoryEvent
{
    /// <summary>
    /// The row's own identity, and the reason it is not a guid.
    ///
    /// <remarks>
    /// A derived event has no row — there is nothing in the database that is "this promotion" —
    /// so its id is built from what it was derived from: the two seasons it sits between. That
    /// makes it stable, which is what lets a client hold on to it across two reads of the page
    /// and know it is looking at the same line both times.
    /// </remarks>
    /// </summary>
    public required string Id { get; init; }

    public required ClubHistoryKind Kind { get; init; }

    /// <summary>
    /// The season the moment belongs to, when it belongs to one.
    /// </summary>
    /// <remarks>
    /// Null for a moment that happened between seasons, or outside the world entirely. A page
    /// that needs to say something about a season it does not have says nothing rather than
    /// naming the nearest one, which would put a rename in the middle of somebody else's year.
    /// </remarks>
    public Guid? SeasonId { get; init; }

    public int? SeasonNumber { get; init; }

    public string? SeasonName { get; init; }

    /// <summary>
    /// What happened, in the words of this codebase.
    /// </summary>
    /// <remarks>
    /// The words are the service's and are written from the facts it derived, and they are
    /// written here rather than stored because a sentence is a reading of a fact and not the
    /// fact: a club with no old name has a rename that cannot be worded, and that is a different
    /// line from one with a bad old name, and only the fact can tell them apart.
    /// </remarks>
    public required string Description { get; init; }

    /// <summary>What the moment carried, when it carried a number: goals in a season, a tier.</summary>
    public int? Value { get; init; }
}

/// <summary>
/// A place on a podium, which is a different claim from the place next to it.
/// </summary>
/// <remarks>
/// "Champion of the 1st division" and "champion of the 3rd" are not the same trophy, and the
/// division is carried with it so a shelf can say which one it is holding rather than a number
/// of medals with nothing to tell them apart.
/// </remarks>
public class ClubTrophy
{
    public required Guid Id { get; init; }

    /// <summary>The competition as the shelf names it.</summary>
    public required string Competition { get; init; }

    public required TrophyKind Kind { get; init; }

    public required Guid SeasonId { get; init; }

    public required int SeasonNumber { get; init; }

    public required string SeasonName { get; init; }

    /// <summary>"1ª Divisão", or null for a competition that is not a division's table.</summary>
    public string? DivisionName { get; init; }

    /// <summary>Which division of the pyramid, counted from one, and null for a cup.</summary>
    public int? DivisionTier { get; init; }
}

/// <summary>
/// A club, whole: who it is, who runs it, what it costs to keep, what it has won, where it
/// has been and what has happened to it.
/// </summary>
/// <remarks>
/// Assembled by the service and not counted by the screen. A page that worked out its own
/// totals would be a second opinion about the pyramid, and the numbers here — the balance, the
/// size of the roster, the times up and down — are all things the world already keeps and is
/// only being read a second time.
/// </remarks>
public class ClubProfile
{
    public required Guid TeamId { get; init; }

    public required string Name { get; init; }

    public required string ShortName { get; init; }

    public required string PrimaryColor { get; init; }

    public required string SecondaryColor { get; init; }

    /// <summary>
    /// The manager's name, or an empty string for a club nobody is in charge of.
    /// </summary>
    /// <remarks>
    /// Empty rather than a stand-in: an NPC club has no manager, and there is a difference
    /// between "nobody runs this club" and "the man who runs it is called Zé Ramalho".
    /// </remarks>
    public string CoachName { get; init; } = string.Empty;

    /// <summary>How many men are on the books, which is not the eleven.</summary>
    public int SquadSize { get; init; }

    /// <summary>What the club has in the bank, in limos.</summary>
    public decimal Balance { get; init; }

    /// <summary>
    /// Times the club has gone up a division and times it has come down one.
    /// </summary>
    /// <remarks>
    /// Worked out by comparing the division it was in last season with the one it is in this
    /// season, because nothing in the world records a move — the pyramid is rebuilt from the
    /// final tables and the only trace a promotion leaves is that the club is not where it was.
    /// </remarks>
    public int Promotions { get; init; }

    public int Relegations { get; init; }

    /// <summary>Everything on the shelf, newest first.</summary>
    public IReadOnlyList<ClubTrophy> Trophies { get; init; } = Array.Empty<ClubTrophy>();

    /// <summary>The club's history, newest first.</summary>
    public IReadOnlyList<ClubHistoryEvent> History { get; init; } = Array.Empty<ClubHistoryEvent>();
}