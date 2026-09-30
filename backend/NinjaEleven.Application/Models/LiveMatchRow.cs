using NinjaEleven.Domain.Enums;
using Match = NinjaEleven.Domain.Matches.Match;

namespace NinjaEleven.Application.Models;

/// <summary>
/// A match that is on the pitch, read together with the window it is part of.
/// </summary>
/// <param name="Match">The match row itself.</param>
/// <param name="RoundId">
/// The window the match belongs to, which is how the scoreboard of a matchday is addressed.
/// It is joined in rather than looked up afterwards: a client following four matches needs
/// four of these every second, and four questions asked of the repository each second is how
/// a live screen starts costing more than the match does.
/// </param>
public record LiveMatchRow(Match Match, Guid RoundId)
{
    /// <summary>Whether the match is somewhere between the whistle and the final one.</summary>
    public bool IsLive => Match.IsLive;

    /// <summary>Whether the match has reached the interval and is waiting there.</summary>
    public bool IsHalfTime => Match.Status is MatchStatus.HalfTime;
}
