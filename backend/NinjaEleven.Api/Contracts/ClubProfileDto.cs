using NinjaEleven.Application.Models;
using NinjaEleven.Domain.Competitions;

namespace NinjaEleven.Api.Contracts;

/// <summary>
/// One moment in a club's life, as the page reads it.
/// </summary>
/// <remarks>
/// The kind arrives as a name and is bound to the enum, so a page can compare it against the
/// word it knows rather than against a number whose meaning depends on the order somebody
/// happened to declare things in. The kinds split into the ones the world derives and the ones
/// it records, and the contract does not tell the client which is which — a client that had to
/// know would be a client deciding where a fact comes from, and where a fact comes from is the
/// backend's business.
/// </remarks>
public class ClubHistoryEventDto
{
    /// <summary>
    /// The moment's identity, stable across two reads of the same page.
    /// </summary>
    /// <remarks>
    /// A string and not a guid because most of a club's history has no row behind it: a
    /// promotion is two season rows compared, and its id is built from the pair. It is stable
    /// anyway, which is what lets a client hold on to it across two reads and know it is
    /// looking at the same line both times.
    /// </remarks>
    public string Id { get; init; } = string.Empty;

    public ClubHistoryKind Kind { get; init; }

    /// <summary>The season the moment belongs to, or null when it belongs to none.</summary>
    public Guid? SeasonId { get; init; }

    public int? SeasonNumber { get; init; }

    public string? SeasonName { get; init; }

    /// <summary>What happened, in this codebase's words.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>What the moment carried, when it carried a number.</summary>
    public int? Value { get; init; }
}

/// <summary>
/// A place on a podium, which is a different claim from the place next to it.
/// </summary>
public class ClubTrophyDto
{
    public Guid Id { get; init; }

    /// <summary>The competition as the shelf names it.</summary>
    public string Competition { get; init; } = string.Empty;

    public TrophyKind Kind { get; init; }

    public Guid SeasonId { get; init; }

    public int SeasonNumber { get; init; }

    public string SeasonName { get; init; } = string.Empty;

    /// <summary>"1ª Divisão", or null for a competition that is not a division's table.</summary>
    public string? DivisionName { get; init; }

    /// <summary>
    /// Which division of the pyramid, counted from one, and null for a cup.
    /// </summary>
    /// <remarks>
    /// The name is for reading and this is for deciding: a shelf holding the 1ª and the 3ª has
    /// to draw two different cups, and a client that recovered the tier out of the name's
    /// leading number would be parsing a label to learn a fact the record already carries.
    /// </remarks>
    public int? DivisionTier { get; init; }
}

/// <summary>
/// A club, whole: who it is, who runs it, what it costs to keep, what it has won, where it
/// has been and what has happened to it.
/// </summary>
public class ClubProfileDto
{
    public Guid TeamId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string ShortName { get; init; } = string.Empty;

    public string PrimaryColor { get; init; } = string.Empty;

    public string SecondaryColor { get; init; } = string.Empty;

    /// <summary>
    /// The manager's name, or an empty string for a club nobody is in charge of.
    /// </summary>
    /// <remarks>
    /// Empty rather than null so a page binds it the same way it binds everything else, and
    /// deliberately not a stand-in: a club with no manager is a club nobody is running, and
    /// filling the gap with a name would be the game inventing a man.
    /// </remarks>
    public string CoachName { get; init; } = string.Empty;

    /// <summary>How many men are on the books, which is not the eleven.</summary>
    public int SquadSize { get; init; }

    /// <summary>What the club has in the bank, in limos.</summary>
    public decimal Balance { get; init; }

    /// <summary>Times the club has gone up a division and times it has come down one.</summary>
    public int Promotions { get; init; }

    public int Relegations { get; init; }

    /// <summary>Everything on the shelf, newest first.</summary>
    public IReadOnlyList<ClubTrophyDto> Trophies { get; init; } = Array.Empty<ClubTrophyDto>();

    /// <summary>The club's history, newest first.</summary>
    public IReadOnlyList<ClubHistoryEventDto> History { get; init; } = Array.Empty<ClubHistoryEventDto>();
}