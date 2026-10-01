using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
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

    /// <summary>
    /// What the match was, worked out from the order the goals went in.
    ///
    /// It travels as a fact rather than as a sentence because a comeback is a fact about a
    /// match: the same 3 x 1 read from four different goal sequences is four different
    /// evenings, and the desk that writes the lead needs to be able to tell them apart.
    /// </summary>
    public MatchShape Shape { get; init; } = MatchShape.Ordinary;

    /// <summary>
    /// Every goal of the match, in the order it happened, for the manager's club and the
    /// other one alike — a report that listed only his own goals would be a report of half
    /// the match.
    /// </summary>
    public IReadOnlyList<MatchGoal> Goals { get; init; } = Array.Empty<MatchGoal>();

    /// <summary>
    /// Where the result left the club, and where it found him.
    ///
    /// <para>
    /// Nulls are meaningful and are not missing data: a cup tie has no table, so both
    /// positions and both gaps are absent, and a report that wrote "7º lugar" about a
    /// quarter-final would be reporting a table nobody is keeping.
    /// </para>
    /// </summary>
    public CompetitionContext? Competition { get; init; }

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

    /// <summary>
    /// Who is out of the next match and why, or null when the whole eleven will be there.
    ///
    /// It travels as facts because a manager reading his box in the morning has to build an
    /// eleven, and a suspension he was not told about is a player he picks on Saturday
    /// afternoon.
    /// </summary>
    public MatchAbsence? Absence { get; init; }

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

/// <summary>
/// The club's week, in the words the treasurer would use.
///
/// <para>
/// The facts arrive already added up — the four buckets and the balance — because the service
/// that owns the book is the one that can read it, and a statement composed by a second reader
/// of the same table would be a second set of numbers for the same week. What is left to the
/// inbox is the writing.
/// </para>
/// </summary>
public class StatementFacts
{
    public required Guid RecipientTeamId { get; init; }
    public required string ClubName { get; init; }

    /// <summary>The first day of the season this statement covers.</summary>
    public required int FromMatchDay { get; init; }

    /// <summary>The last day of the season this statement covers, the day it was written on.</summary>
    public required int ToMatchDay { get; init; }

    public required decimal GateRevenue { get; init; }
    public required decimal Signings { get; init; }
    public required decimal Sales { get; init; }
    public required decimal OtherIncome { get; init; }

    public required decimal Wages { get; init; }
    public required decimal Training { get; init; }
    public required decimal OtherExpenses { get; init; }

    /// <summary>What the club had when the week opened.</summary>
    public required decimal OpeningBalance { get; init; }

    /// <summary>What the club has now, which is the last line's own balance.</summary>
    public required decimal ClosingBalance { get; init; }

    /// <summary>
    /// The gate count, so the statement can say whether a full house or an empty one paid for
    /// it. A manager looking at L$ 40.000 of receipts wants to know whether that was a crowd
    /// or a ticket price, and those are two different decisions.
    /// </summary>
    public int HomeMatches { get; init; }

    /// <summary>The reference the statement is written once under.</summary>
    public required string Reference { get; init; }
}

/// <summary>
/// A sponsor has signed with the club, in the words a manager reads it in.
///
/// <para>
/// A deal is signed by hand and a deal that ends is signed by nobody, which is why the two
/// are separate messages and neither is folded into the money: what a manager needs to know
/// about a sponsor is the name on the shirt and the number of matches it lasts, and both are
/// facts about a contract rather than about a line of the book.
/// </para>
/// </summary>
public class SponsorSignedFacts
{
    public required Guid RecipientTeamId { get; init; }
    public required string ClubName { get; init; }
    public required Guid SponsorId { get; init; }
    public required string SponsorName { get; init; }
    public required Guid ContractId { get; init; }

    /// <summary>What the sponsor pays per match, which is the shape of the whole deal.</summary>
    public required decimal PerMatchFee { get; init; }

    /// <summary>How many matches the shirt carries the name for.</summary>
    public required int ContractMatches { get; init; }
}

/// <summary>
/// Where a result left the club, in the words a manager reads it in.
///
/// <para>
/// The four numbers a report needs and the four sentences it cannot invent: the position the
/// club held before the whistle, the one it is holding now, how far it is from the place it
/// wants and how far from the place it does not want to be. Every one of them is read from
/// the table as it stands, and every one of them is absent for a competition that has no
/// table — a cup tie is decided by the tie, not by a position.
/// </para>
/// </summary>
public class CompetitionContext
{
    /// <summary>"Campeonato", "Copa", "Supercopa" — what kind of competition this was.</summary>
    public required CompetitionType Kind { get; init; }

    /// <summary>True when the competition keeps a table of sixteen clubs.</summary>
    public bool HasTable { get; init; }

    /// <summary>Where the club stood before this match, counted from one.</summary>
    public int? PositionBefore { get; init; }

    /// <summary>Where it stands now, counted from one.</summary>
    public int? PositionAfter { get; init; }

    /// <summary>Places climbed, or the number of places dropped.</summary>
    public int PositionChange { get; init; }

    public int? Points { get; init; }

    /// <summary>
    /// The points that separate the club from the last place that goes up, and from the
    /// first place that goes down. Null when there is no such place — a club already inside
    /// the promotion places is not a fixed number of points away from qualifying, it is
    /// there.
    /// </summary>
    public int? PointsToPromotion { get; init; }

    public int? PointsToRelegation { get; init; }

    /// <summary>How many rounds of the championship are still to be played.</summary>
    public int? RoundsRemaining { get; init; }

    /// <summary>The round this match was, counted from one, when the competition keeps rounds.</summary>
    public int? RoundNumber { get; init; }

    /// <summary>
    /// Whether the tie was won on the night — decided on the aggregate, or on penalties —
    /// and null when it was not decided by this match.
    /// </summary>
    public bool? Advanced { get; init; }
}

/// <summary>
/// Why a man will not be there next week, kept apart because a manager acts on them
/// differently: a red and an accumulation of yellows are both decided, and an injury is the
/// one thing about which the only decision left is who replaces him.
/// </summary>
public enum AbsenceCause
{
    /// <summary>A straight red. Two matches.</summary>
    RedCard,

    /// <summary>Three yellows in one match. Two matches, and the count starts again.</summary>
    AccumulatedYellows,

    /// <summary>Hurt in the match, and hurt enough to have come off.</summary>
    Injury
}

/// <summary>One man out of the next match.</summary>
public class PlayerAbsence
{
    public required Guid PlayerId { get; init; }
    public required AbsenceCause Cause { get; init; }

    /// <summary>
    /// How many matches he is out, or zero when the number is not the engine's to say — a
    /// knock drawn in the match itself is measured in days the club does not control.
    /// </summary>
    public int Matches { get; init; }
}

/// <summary>
/// The men who will be missing from the next match, and why.
///
/// <para>
/// A report that names them matters more than a report that says "the referee was busy": a
/// manager reading his box on the morning after has to build an eleven, and a suspension he
/// was not told about is a player he picks on Saturday afternoon. The two causes that carry
/// a number are the ones that decide the shape of that eleven, so the number is carried too
/// rather than left for the manager to go and count.
/// </para>
/// </summary>
public class MatchAbsence
{
    public IReadOnlyList<PlayerAbsence> Players { get; init; } = Array.Empty<PlayerAbsence>();
}
