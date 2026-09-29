using NinjaEleven.Domain.Inbox;

namespace NinjaEleven.Application.Models;

/// <summary>
/// One name a message uses, and the thing it belongs to, as the screen is given it.
/// </summary>
public class InboxPersonDto
{
    public string Name { get; init; } = string.Empty;
    public string Kind { get; init; } = InboxMentionKind.Team;
    public Guid Id { get; init; }
}

/// <summary>One line of a club's box, in the order it arrived.</summary>
public class InboxMessageLine
{
    public Guid Id { get; init; }
    public InboxCategory Category { get; init; }
    public string Subject { get; init; } = string.Empty;
    public string SenderName { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public IReadOnlyList<InboxPersonDto> Mentions { get; init; } = Array.Empty<InboxPersonDto>();
    public string? LinkLabel { get; init; }
    public string? LinkRoute { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public bool IsRead { get; init; }
    public DateTimeOffset? ReadAt { get; init; }
}

/// <summary>
/// A page of a club's box.
///
/// The unread count travels with the page and not as its own number, because the two are
/// asked for at the same moment and by the same screen: a manager opening his own mail
/// already knows how much of it is new, and a second request to find out is a request that
/// can be answered a second later than the page it belongs to.
/// </summary>
public class InboxBox
{
    public IReadOnlyList<InboxMessageLine> Messages { get; init; } = Array.Empty<InboxMessageLine>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
    public int UnreadCount { get; init; }
}

/// <summary>
/// How a match ended, in the words a report is written in.
///
/// The facts arrive here already worked out — the score, the eleven, the goals, the cards,
/// where the next one is — because the service that knows the match is the one that settles
/// it, and a report composed by a second reader of the same tables would be a second account
/// of the same afternoon. What is left to <see cref="InboxService"/> is the writing.
/// </summary>
public class MatchReportFacts
{
    public required Guid MatchId { get; init; }
    public required Guid RecipientTeamId { get; init; }
    public required Guid ClubId { get; init; }
    public required string ClubName { get; init; }
    public required Guid OpponentId { get; init; }
    public required string OpponentName { get; init; }

    public bool IsHome { get; init; }

    /// <summary>"1ª Divisão", "Copa" — the edition's own name.</summary>
    public required string CompetitionName { get; init; }

    /// <summary>"Rodada 9", "Oitavas de final" — the phase the match was played in.</summary>
    public required string PhaseName { get; init; }

    /// <summary>"partida de volta", when the tie has two legs.</summary>
    public string? LegLabel { get; init; }

    public int? MatchDayNumber { get; init; }

    public required int ClubGoals { get; init; }
    public required int OpponentGoals { get; init; }

    public string ClubFormation { get; init; } = string.Empty;
    public string OpponentFormation { get; init; } = string.Empty;

    /// <summary>
    /// The eleven the club put out, in the order the pitch had them. A report that listed
    /// them alphabetically would say nothing about the team that played.
    /// </summary>
    public IReadOnlyList<InboxPersonDto> Lineup { get; init; } = Array.Empty<InboxPersonDto>();

    public IReadOnlyList<InboxPersonDto> Substitutes { get; init; } = Array.Empty<InboxPersonDto>();

    /// <summary>A goal, in the sentence the match announced it with.</summary>
    public IReadOnlyList<string> GoalLines { get; init; } = Array.Empty<string>();

    /// <summary>Every name the goals mention, so each of them is a door.</summary>
    public IReadOnlyList<InboxPersonDto> GoalScorers { get; init; } = Array.Empty<InboxPersonDto>();

    public IReadOnlyList<InboxPersonDto> Booked { get; init; } = Array.Empty<InboxPersonDto>();

    /// <summary>Where the club plays next, and against whom.</summary>
    public Guid? NextFixtureId { get; init; }
    public string? NextOpponentName { get; init; }
    public Guid? NextOpponentId { get; init; }
    public int? NextMatchDayNumber { get; init; }
    public string? NextCompetitionName { get; init; }
    public string? NextStadiumName { get; init; }
    public bool NextIsHome { get; init; }
}

/// <summary>
/// Somebody has made an offer for one of the club's players, in the words a manager reads it.
/// </summary>
public class TransferOfferFacts
{
    public required Guid RecipientTeamId { get; init; }
    public required string ClubName { get; init; }
    public required Guid PlayerId { get; init; }
    public required string PlayerName { get; init; }
    public Guid? BiddingClubId { get; init; }
    public string? BiddingClubName { get; init; }
    public required decimal Fee { get; init; }

    /// <summary>
    /// What the book says the player is worth. A manager deciding whether to sell reads the
    /// two numbers side by side, and a report that quoted only one of them would be quoting
    /// the one that flatters the deal.
    /// </summary>
    public decimal? AskingPrice { get; init; }

    /// <summary>What the club is being offered, in the words a manager would use.</summary>
    public string? Reference { get; init; }
}

/// <summary>
/// How a shirt deal ended, in the words a manager reads it in.
///
/// A contract is not a date: it is a number of matches, and it is paid for one at a time. The
/// last one is the news, because a club whose deal has run out is a club playing the next
/// match with nobody's name on the shirt, and the only place it is ever told is the matchday
/// after the one that paid the final instalment.
/// </summary>
public class SponsorExpiryFacts
{
    public required Guid RecipientTeamId { get; init; }
    public required string ClubName { get; init; }
    public required Guid SponsorId { get; init; }
    public required string SponsorName { get; init; }
    public required Guid ContractId { get; init; }

    /// <summary>What the sponsor was paying per match, which is what stops when the deal ends.</summary>
    public required decimal PerMatchFee { get; init; }

    /// <summary>How many matches the deal was signed for, and how many were actually paid.</summary>
    public required int ContractMatches { get; init; }
}

/// <summary>
/// What the market decided about a proposal, and why.
///
/// A manager who bids for a player is left holding a question for as long as the proposal is
/// pending, and the answer arrives in three shapes: the other club said yes, the other club
/// said no, or the other club said nothing until the market stopped waiting. They are not the
/// same thing and a box that flattened them into "the deal did not happen" would be a box
/// that never told him whether he was outbid or out-played.
/// </summary>
public enum InboxDecision
{
    /// <summary>The other club agreed to the price.</summary>
    Accepted,

    /// <summary>The price was under the card, and no roll was cast on it.</summary>
    RefusedOnPrice,

    /// <summary>The price was right and the club weighed the man and kept him.</summary>
    RefusedOnThePlayer,

    /// <summary>The club is down to the minimum and cannot sell anybody else.</summary>
    RefusedOnTheSquad,

    /// <summary>Nobody answered, and the market stopped waiting.</summary>
    Expired,

    /// <summary>It was agreed, and then it could not be kept.</summary>
    CalledOff
}

/// <summary>
/// The answer to a proposal, in the words a manager reads it in.
///
/// The role is in the facts rather than worked out from the clubs, because the same decision
/// read from the two sides is two different sentences: "aceitou a proposta" is what a buyer
/// reads and "vendeu o jogador" is what a seller reads, and a single sentence trying to be
/// both is a sentence about nobody.
/// </summary>
public class TransferDecisionFacts
{
    public required Guid RecipientTeamId { get; init; }
    public required string ClubName { get; init; }
    public required Guid PlayerId { get; init; }
    public required string PlayerName { get; init; }

    /// <summary>The club on the other side of the deal, named however the manager knows it.</summary>
    public required Guid OtherClubId { get; init; }
    public required string OtherClubName { get; init; }

    public required decimal Fee { get; init; }
    public required InboxDecision Outcome { get; init; }

    /// <summary>
    /// True when it is the manager's own player being let go, false when it is his own bid
    /// being answered.
    /// </summary>
    public required bool ManagerIsSeller { get; init; }

    /// <summary>The proposal this answers. The outcome is folded into the written reference.</summary>
    public required string Reference { get; init; }
}
