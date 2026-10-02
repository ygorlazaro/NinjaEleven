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
///
/// <para>
/// The tally travels with it for the same reason. A screen drawing a filter per category has
/// to label each one with how much of the box is behind it, and that number is the same for
/// every page of the same box — a filter column renumbering itself on every page turn is a
/// column of numbers that cannot be compared to each other.
/// </para>
/// </summary>
public class InboxBox
{
    public IReadOnlyList<InboxMessageLine> Messages { get; init; } = Array.Empty<InboxMessageLine>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
    public int UnreadCount { get; init; }

    /// <summary>
    /// The kind this page was filtered by, or null for the whole box.
    ///
    /// It is echoed back because the screen's buttons have to know which of them is the one
    /// that is on, and a client that kept that state to itself would light up a filter the
    /// server is not applying after any navigation it did not perform itself.
    /// </summary>
    public InboxCategory? Category { get; init; }

    /// <summary>How many of each kind the box holds, and it is the whole box.</summary>
    public IReadOnlyList<InboxCategoryTally> Categories { get; init; } = Array.Empty<InboxCategoryTally>();
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

    /// <summary>
    /// The round by which the offer has to be answered, when it has one.
    ///
    /// <para>
    /// It is on the message because a manager deciding whether to sell needs to know whether he
    /// is deciding in a week or not deciding at all, and a report that said a deadline was
    /// running against a proposal with no deadline is a manager holding off for a clock that was
    /// never going to ring. An offer the engine made on its own has none: it waits on the club
    /// rather than the club waiting on a round.
    /// </para>
    /// </summary>
    public int? AnswerByRound { get; init; }

    /// <summary>What the club is being offered, in the words a manager would use.</summary>
    public string? Reference { get; init; }
}

/// <summary>
/// One of a manager's players arriving at a rival, in the words the manager reads it in.
/// </summary>
/// <remarks>
/// <para>
/// It is a departure told from the other side. A manager who sold reads a cheque and a
/// transaction; a manager who watched his striker sign a neighbour reads neither, and is the
/// one who is actually planning a season around that player's goals — so the news is his, and
/// the message that carries it is written as a departure from his own squad.
/// </para>
/// <para>
/// The tier travels because the message says "the same division as yours" and says which one,
/// and because a reader who did not know it would take "2ª Divisão" for the second line of a
/// table rather than the pyramid.
/// </para>
/// </remarks>
public class PlayerDepartureFacts
{
    /// <summary>The club that lost the player, which is the club the message is delivered to.</summary>
    public required Guid RecipientTeamId { get; init; }

    public required string ClubName { get; init; }
    public required Guid PlayerId { get; init; }
    public required string PlayerName { get; init; }

    /// <summary>Where he went; null when the buyer is a club the world did not name.</summary>
    public Guid? BuyingTeamId { get; init; }

    public string? BuyingClubName { get; init; }

    /// <summary>Which division of the pyramid both clubs are in, counted from one.</summary>
    public required int DivisionTier { get; init; }

    /// <summary>What the deal was worth to the club that sold him.</summary>
    public required decimal Fee { get; init; }

    /// <summary>
    /// The deal this message is about, so a window closed twice is one departure told about
    /// once.
    /// </summary>
    public required Guid TransferId { get; init; }

    public string? Reference { get; init; }
}

/// <summary>
/// One tie of a cup round, in the words the round's news is written in.
/// </summary>
/// <remarks>
/// The winner is a field rather than something worked out from the two sides and the score,
/// because the aggregate is what decided it and a reader who added the two legs up from one
/// score would get a different answer from the one the game settled.
/// </remarks>
public class CupRoundTieFacts
{
    public required Guid HomeTeamId { get; init; }
    public required string HomeClubName { get; init; }
    public required int HomeGoals { get; init; }

    public required Guid AwayTeamId { get; init; }
    public required string AwayClubName { get; init; }
    public required int AwayGoals { get; init; }

    /// <summary>
    /// Who goes through, and null only for a tie that is still level — which a round that is
    /// being announced is not.
    /// </summary>
    public Guid? WinnerTeamId { get; init; }

    /// <summary>Whether the tie needed penalties to be decided.</summary>
    public bool WentToPenalties { get; init; }

    /// <summary>Whether the tie was two legs rather than one.</summary>
    public bool IsSecondLeg { get; init; }
}

/// <summary>
/// A cup round that has been settled, told to everybody who is running a club.
/// </summary>
/// <remarks>
/// <para>
/// It has no recipient, and that is the whole difference from every other message here. A cup
/// round is the one piece of football that belongs to the country: sixty-four clubs, one
/// bracket, and a manager whose club is not in it on this afternoon still watched it, because
/// the round of sixteen is where the season's biggest names start falling out and a manager
/// planning his own summer needs to know which of them are gone.
/// </para>
///
/// <para>
/// It is still delivered only to a club with a person behind it — the rule is the same one,
/// and it is asked of the world rather than of the request, so a scheduler walking the cup
/// tells the same managers a hand pressing the button would.
/// </para>
/// </remarks>
public class CupRoundFacts
{
    /// <summary>The edition the round belongs to, which is also what the round is identified by.</summary>
    public required Guid CompetitionSeasonId { get; init; }

    public required string CupName { get; init; }
    public required string SeasonName { get; init; }

    /// <summary>The round's number in the bracket, counted from the first round.</summary>
    public required int RoundNumber { get; init; }

    public required IReadOnlyList<CupRoundTieFacts> Ties { get; init; }

    /// <summary>
    /// What makes this message once: the cup and the round. A round closed twice — by the run
    /// that played it and by a process that was down over the weekend — is one round of news.
    /// </summary>
    public string Reference => $"cup-round:{CompetitionSeasonId}:{RoundNumber}";
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
/// A player is in the last year of his contract, in the words a manager reads it in.
/// </summary>
public class ContractExpiringFacts
{
    public required Guid RecipientTeamId { get; init; }
    public required string ClubName { get; init; }
    public required Guid PlayerId { get; init; }
    public required string PlayerName { get; init; }
    public required Guid ContractId { get; init; }

    /// <summary>How many seasons are left, which on this message is always one.</summary>
    public required int SeasonsLeft { get; init; }

    /// <summary>What the club is paying him now.</summary>
    public required decimal Wage { get; init; }

    /// <summary>
    /// What he would be paid if the club signed him again today, which is the number a
    /// manager is being asked to weigh.
    /// </summary>
    public required decimal WageOnRenewal { get; init; }
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

/// <summary>
/// A youth academy player who grew in his potential this round, in the words a manager reads it in.
/// </summary>
public class AcademyEvolutionFacts
{
    public required Guid RecipientTeamId { get; init; }
    public required string ClubName { get; init; }
    public required Guid PlayerId { get; init; }
    public required string PlayerName { get; init; }
    public required string Attribute { get; init; }
    public required int Gained { get; init; }
    public required int Before { get; init; }
    public required int After { get; init; }
}

/// <summary>
/// A player the manager has placed on the transfer list, in the words a manager reads it.
/// </summary>
public class TransferListedFacts
{
    public required Guid RecipientTeamId { get; init; }
    public required string ClubName { get; init; }
    public required Guid PlayerId { get; init; }
    public required string PlayerName { get; init; }
}

/// <summary>
/// A player who has announced he will retire at the end of the season, in the words a manager
/// reads it in.
///
/// The announcement is a rule applied at the season boundary, not a decision a manager makes — so
/// the message is written once per player, keyed by the contract and the season, and it arrives
/// at the same moment as the one warning that his deal is also in its last year.
/// </summary>
public class RetirementAnnouncedFacts
{
    public required Guid RecipientTeamId { get; init; }
    public required string ClubName { get; init; }
    public required Guid PlayerId { get; init; }
    public required string PlayerName { get; init; }

    /// <summary>
    /// The season this announcement applies to, for the reference so the message is written once
    /// per player per season.
    /// </summary>
    public required Guid SeasonId { get; init; }
}

/// <summary>
/// The market opening or closing, in the words a manager reads it in.
/// </summary>
/// <remarks>
/// <para>
/// A window is a date with consequences, and the two dates a manager cannot look up — when it
/// shuts, and when a man signed today actually walks through the door — are the two things the
/// screen does not put in a sentence. The message carries both, because a manager deciding
/// whether to bid needs to know whether he is deciding this week or whether he is deciding for
/// the first round of next season.
/// </para>
///
/// <para>
/// Open and closed are the same method rather than two, because they are the same fact seen
/// from two sides and the difference between them is a sentence. They are nevertheless two
/// references: a club told its window is open and a club told its window has shut is two pieces
/// of news, and one of them at the same round is the other of them one round later.
/// </para>
/// </remarks>
public class TransferWindowFacts
{
    public required Guid RecipientTeamId { get; init; }
    public required string ClubName { get; init; }

    /// <summary>The season the market belongs to, as a manager would say it.</summary>
    public required Guid SeasonId { get; init; }

    public required string SeasonName { get; init; }

    /// <summary>Whether the window is open on the round this message is about.</summary>
    public required bool IsOpen { get; init; }

    /// <summary>
    /// The championship round this message is about, counted from one — the round whose closing
    /// opens or shuts the window.
    /// </summary>
    public required int RoundNumber { get; init; }

    /// <summary>
    /// The round the deals signed in this window walk in on. It is the round's own number and
    /// not a label, because the whole point of the sentence is that a manager can plan a squad
    /// around it.
    /// </summary>
    public required int ArrivalRound { get; init; }

    /// <summary>
    /// The round of the next window, or null when this is the last one there is. A null is a
    /// fact rather than missing data: after the second window the next arrival is the Supercup of
    /// the following season, and it does not belong to any round of the one being reported.
    /// </summary>
    public int? NextWindowRound { get; init; }
}

/// <summary>
/// A man who will not be on the pitch, and the discipline that put him there, on its own.
/// </summary>
/// <remarks>
/// <para>
/// It is a separate message from the report of the match and not a paragraph of it. A report
/// says what happened over ninety minutes and a manager reads it the same evening; a suspension
/// is a fact about the next two or three fixtures and he reads it in the morning, with the
/// lineup screen open. Told once, it lands where it is read.
/// </para>
///
/// <para>
/// It carries only the two causes that suspend. An injury is measured in days the club does not
/// control and is a different kind of absence entirely — the one where the only decision left is
/// who replaces him — so it travels in <see cref="AbsenceCause.Injury"/> and is not the business
/// of this message.
/// </para>
/// </remarks>
public class SuspensionFacts
{
    public required Guid RecipientTeamId { get; init; }
    public required string ClubName { get; init; }
    public required Guid PlayerId { get; init; }
    public required string PlayerName { get; init; }

    /// <summary>
    /// How many matches he misses. It is the number a manager schedules around, so it is carried
    /// rather than left for him to count on the suspensions screen.
    /// </summary>
    public required int Matches { get; init; }

    /// <summary>The discipline, and not a sentence: a red and three yellows are read differently.</summary>
    public required AbsenceCause Cause { get; init; }

    /// <summary>
    /// The match the card was shown in, and the whole of what makes the reference: the same
    /// player shown twice in one match is one suspension and not two.
    /// </summary>
    public required Guid MatchId { get; init; }

    public Guid? OpponentId { get; init; }
    public string? OpponentName { get; init; }

    /// <summary>
    /// Where it happened, in one phrase — "3ª rodada, contra o Grêmio". It travels already
    /// written because the caller is the piece of the world that knows the round and the
    /// competition, and a message that named the round from a number it was handed would have two
    /// ways of saying the same afternoon.
    /// </summary>
    public string? MatchLabel { get; init; }
}

/// <summary>
/// A player who has just walked into the club, in the words a manager reads it in.
/// </summary>
/// <remarks>
/// The counterpart of the departure, and told with the same appetite for what the man is: the
/// fee is what the club paid, the wage is what it will pay every season, and the book value is
/// what the club's own department thinks he is worth. A signing told without the third of those
/// is an advertisement, and this box does not carry advertisements.
/// </remarks>
public class SigningFacts
{
    public required Guid RecipientTeamId { get; init; }
    public required string ClubName { get; init; }
    public required Guid PlayerId { get; init; }
    public required string PlayerName { get; init; }

    public required int Age { get; init; }

    /// <summary>
    /// The wire position: "GK", "DEF", "MID" or "ATT". The word a sentence needs is worked out
    /// from it by the same rule for every message, because the box does not carry four ways of
    /// spelling the same position.
    /// </summary>
    public required string Position { get; init; }

    public Guid? SellingClubId { get; init; }
    public string? SellingClubName { get; init; }

    /// <summary>Which division of the pyramid the club he came from is in, counted from one.</summary>
    public int? SellingDivisionTier { get; init; }

    /// <summary>What the club paid, and zero when he was a free agent.</summary>
    public required decimal Fee { get; init; }

    /// <summary>
    /// Whether he came free. It is on the facts rather than worked out from the fee because a
    /// club's own signing for no money is a different piece of news from a player with no club,
    /// and only the caller knows which of the two it has.
    /// </summary>
    public required bool IsFreeAgent { get; init; }

    /// <summary>The new contract's wage per season, which is the shape of the deal.</summary>
    public required decimal Wage { get; init; }

    /// <summary>
    /// What the book says he is worth, when the book says anything. It travels beside the fee so
    /// a manager can weigh one against the other without leaving the message.
    /// </summary>
    public decimal? MatchValue { get; init; }

    /// <summary>
    /// The deal he arrived on. It is what makes the message once: a window closed twice by a run
    /// that played it and by a process that was down over the weekend is one arrival.
    /// </summary>
    public required Guid TransferId { get; init; }
}

/// <summary>
/// A club that has changed division between two seasons, in the words it is told in.
/// </summary>
/// <remarks>
/// It is one message for both directions because the pyramid is one thing: the club that goes up
/// and the club that comes down are two lines of the same ladder, and the size of the step is the
/// same number in either reading. What differs is only which way the sentence walks.
/// </remarks>
public class DivisionMovementFacts
{
    public required Guid RecipientTeamId { get; init; }
    public required string ClubName { get; init; }

    /// <summary>The season whose last table decided it, which is also what makes it once.</summary>
    public required Guid SeasonId { get; init; }

    public required string SeasonName { get; init; }

    /// <summary>True when the club goes up the pyramid, false when it comes down it.</summary>
    public required bool Promoted { get; init; }

    /// <summary>"2ª Divisão" — the division it played in, named whole.</summary>
    public required string FromDivisionName { get; init; }

    /// <summary>"1ª Divisão" — the division it will play in next season, named whole.</summary>
    public required string ToDivisionName { get; init; }

    /// <summary>Where it finished in the old division, counted from one.</summary>
    public required int Position { get; init; }

    public required int Points { get; init; }
    public required int Played { get; init; }
    public required int Wins { get; init; }
    public required int Draws { get; init; }
    public required int Losses { get; init; }
    public required int GoalsFor { get; init; }
    public required int GoalsAgainst { get; init; }
}

/// <summary>
/// A club that cannot move a man because it has nobody left to spare, in the words it is told in.
/// </summary>
/// <remarks>
/// It is worth a message because the block is invisible until it refuses something. A club of
/// twenty-one players looks exactly like a club of twenty-three on a table, and the first sign
/// that the difference matters is a rejected release and a rejected sale — two answers a manager
/// reads as somebody else's decision.
/// </remarks>
public class SquadFloorFacts
{
    public required Guid RecipientTeamId { get; init; }
    public required string ClubName { get; init; }

    /// <summary>The season, which is also the whole of the reference.</summary>
    public required Guid SeasonId { get; init; }

    public required string SeasonName { get; init; }

    public required int SquadSize { get; init; }

    /// <summary>
    /// The floor itself, carried rather than written into the sentence. The number belongs to
    /// <c>SquadSizeRules</c>, and a message that spelled out "21" would be a second copy of it
    /// that a later retune of the rule would leave behind.
    /// </summary>
    public required int MinSquadSize { get; init; }
}

/// <summary>
/// One tie of a cup draw, before a ball of it has been kicked.
/// </summary>
/// <remarks>
/// <para>
/// A draw has no winners and no losers, so unlike <see cref="CupRoundTieFacts"/> it carries no
/// winner: what it carries is what is already known about the tie, which is usually nothing and
/// occasionally the first leg.
/// </para>
///
/// <para>
/// The scores are nullable per leg rather than per tie on purpose. A draw drawn at the start of
/// the campaign has none; a message about a round whose first leg was played by the time it was
/// written has one. Printing two empty columns of zeros would be a news item about a game that
/// has not happened.
/// </para>
/// </remarks>
public class CupDrawTie
{
    public required Guid HomeTeamId { get; init; }
    public required string HomeName { get; init; }

    public required Guid AwayTeamId { get; init; }
    public required string AwayName { get; init; }

    /// <summary>The first leg's goals, when the first leg has been played.</summary>
    public int? HomeLegScore { get; init; }

    public int? AwayLegScore { get; init; }

    /// <summary>
    /// The tie settled, said in words: "2-1 no agregado". It is the fact rather than two numbers
    /// the reader would have to add up himself, and it is null while the tie is undecided.
    /// </summary>
    public string? AggregateAwayHomeLabel { get; init; }
}

/// <summary>
/// A cup round drawn, told to every manager in the country.
/// </summary>
/// <remarks>
/// <para>
/// It has no recipient, and that is the whole difference from every other message here. A draw is
/// the country's news: a manager whose club was not drawn still has to know which of the names on
/// the television went out of the cup, because half of them are the clubs his own players will be
/// measured against in ten weeks.
/// </para>
/// </remarks>
public class CupDrawFacts
{
    /// <summary>The edition the round belongs to, which is also what the draw is identified by.</summary>
    public required Guid CupEditionId { get; init; }

    /// <summary>"Copa Ninja Eleven" — the competition the draw is a round of.</summary>
    public required string CompetitionName { get; init; }

    /// <summary>The round's number in the bracket, counted from the first round.</summary>
    public required int RoundNumber { get; init; }

    /// <summary>
    /// "32 avos de final" — the round as a manager would say it. It is carried so the caller can
    /// say the round the draw calls itself rather than the box deciding it, and the message falls
    /// back to the rules' own name when it arrives blank.
    /// </summary>
    public string RoundName { get; init; } = string.Empty;

    public required IReadOnlyList<CupDrawTie> Ties { get; init; }

    /// <summary>
    /// The calendar day the first leg is played on, when the round's schedule says one. It is the
    /// date a manager writes on the sheet, and a message about a draw that omits it leaves the
    /// only actionable fact of the whole thing unsaid.
    /// </summary>
    public int? FirstLegMatchDay { get; init; }
}

/// <summary>
/// A youth player brought up into the first team, in the words a manager reads it in.
/// </summary>
/// <remarks>
/// It is told because the promotion is a decision with a price attached — a one-season contract at
/// the minimum wage — and because the honest half of it is that nobody yet knows whether the boy
/// will play. A message that only carried the arrival would be the same message as a signing, and
/// it is not one.
/// </remarks>
public class AcademyPromotionFacts
{
    public required Guid RecipientTeamId { get; init; }
    public required string ClubName { get; init; }
    public required Guid PlayerId { get; init; }
    public required string PlayerName { get; init; }

    /// <summary>The wire position: "GK", "DEF", "MID" or "ATT".</summary>
    public required string Position { get; init; }

    public required int Age { get; init; }

    /// <summary>The wage the one-season contract pays, which is the floor the club gives.</summary>
    public required decimal Wage { get; init; }

    /// <summary>
    /// The ceiling the player can grow to, which is the number a manager is actually being asked
    /// to bet on.
    /// </summary>
    public required int Potential { get; init; }
}

/// <summary>
/// A contract signed again, in the words a manager reads it in.
/// </summary>
/// <remarks>
/// It is told after the fact and never before it, because there is nothing to announce while the
/// renewal is a screen with four buttons on it: what a manager cannot see from the squad table is
/// what the new deal does to the wage bill, and that is what this says.
/// </remarks>
public class ContractRenewedFacts
{
    public required Guid RecipientTeamId { get; init; }
    public required string ClubName { get; init; }
    public required Guid PlayerId { get; init; }
    public required string PlayerName { get; init; }

    /// <summary>The contract that was renewed, which is the whole of the reference.</summary>
    public required Guid ContractId { get; init; }

    /// <summary>How many seasons this renewal just signed.</summary>
    public required int Seasons { get; init; }

    /// <summary>What the club was paying per season before, which is the cost of the alternative.</summary>
    public required decimal PreviousWage { get; init; }

    /// <summary>What it pays per season now.</summary>
    public required decimal NewWage { get; init; }

    /// <summary>How many of those seasons are still to be served after this one.</summary>
    public required int NewSeasonsLeft { get; init; }

    /// <summary>
    /// The whole length of the contract, seasons already served included. It is what turns a wage
    /// into a liability: a wage for one season is a cost, and the same wage for five is a plan.
    /// </summary>
    public required int NewTotalSeasons { get; init; }
}

/// <summary>
/// One company on the shortlist of companies that would put their name on the shirt.
/// </summary>
/// <remarks>
/// It is the row of the sponsors screen and not the sponsor: the shortlist is a decision with
/// several prices in it, and the box quotes the best of them rather than carrying all of them as
/// prose that would go stale the moment the next window is drawn.
/// </remarks>
public class SponsorCandidateFacts
{
    public required Guid SponsorId { get; init; }
    public required string SponsorName { get; init; }

    /// <summary>
    /// "Local", "Regional" or "Nacional" — the size of the company as the sponsors screen writes
    /// it. It travels as words rather than as a weight because the weight is the rule's number
    /// and the sentence needs the name of the thing.
    /// </summary>
    public string SizeLabel { get; init; } = string.Empty;

    public required decimal PerMatchFee { get; init; }

    /// <summary>How many matches the shirt would carry the name for.</summary>
    public required int ContractMatches { get; init; }

    /// <summary>
    /// The term of the offer in words — how long it stands, or what happens when it ends. Blank
    /// when the company imposes no term of its own, and a message says so rather than filling it.
    /// </summary>
    public string? ExpiryLabel { get; init; }
}

/// <summary>
/// The companies that would take the shirt this round, in the words a manager reads them in.
/// </summary>
/// <remarks>
/// It is the shortlist rather than a signed deal, which is the whole difference from
/// <see cref="SponsorSignedFacts"/>: nobody has agreed anything here and the manager is the one
/// who agrees. What he is owed is the best price on the table and the fact that the list is not
/// a draw — a board of three that redrew itself every visit would make a decision taken on one
/// visit a decision taken on nothing.
/// </remarks>
public class SponsorBookFacts
{
    public required Guid RecipientTeamId { get; init; }
    public required string ClubName { get; init; }

    public required Guid SeasonId { get; init; }
    public required string SeasonName { get; init; }

    /// <summary>The round this shortlist was drawn on, which is half of the reference.</summary>
    public required int RoundNumber { get; init; }

    /// <summary>
    /// True when the club has no deal at all and is being offered one for the first time; false
    /// when it has one that is running out and is being offered a renewal. The two are different
    /// news and the message says which one it is.
    /// </summary>
    public required bool IsFirstDraw { get; init; }

    /// <summary>
    /// Every company that would take the club, ordered by what it pays. It is the whole list and
    /// not a drawn shortlist: the number on the screen is the screen's, and the order is the
    /// price.
    /// </summary>
    public required IReadOnlyList<SponsorCandidateFacts> Candidates { get; init; }
}

/// <summary>
/// One line of a division's table after a matchday, as a message reads it.
/// </summary>
/// <remarks>
/// It carries the position it held yesterday as well as the one it holds now, because the two
/// numbers are the news and either of them alone is a table. Yesterday is null for a club that
/// was not in this division yesterday, which is a fact about the club and not a missing row.
/// </remarks>
public class RoundSummaryLine
{
    public required Guid TeamId { get; init; }
    public required string ClubName { get; init; }

    /// <summary>Where the club stands now, counted from one.</summary>
    public required int Position { get; init; }

    /// <summary>Where it stood before the round, or null when it was not in this division then.</summary>
    public int? PreviousPosition { get; init; }

    public required int Points { get; init; }
    public required int Played { get; init; }
    public required int Wins { get; init; }
    public required int Draws { get; init; }
    public required int Losses { get; init; }
    public required int GoalsFor { get; init; }
    public required int GoalsAgainst { get; init; }
}

/// <summary>
/// One division's table after a matchday, as a message reads it.
/// </summary>
public class RoundSummaryDivision
{
    /// <summary>"1ª Divisão" — the division's own name, top first where there are several.</summary>
    public required string DivisionName { get; init; }

    public required IReadOnlyList<RoundSummaryLine> Lines { get; init; }
}

/// <summary>
/// One result of the day, as a message reads it.
/// </summary>
public class RoundSummaryFixture
{
    public required Guid MatchId { get; init; }

    public required Guid HomeTeamId { get; init; }
    public required string HomeName { get; init; }

    public required Guid AwayTeamId { get; init; }
    public required string AwayName { get; init; }

    public required int HomeGoals { get; init; }
    public required int AwayGoals { get; init; }

    /// <summary>"1ª Divisão", "Copa Ninja Eleven" — which competition the result belongs to.</summary>
    public required string CompetitionName { get; init; }
}

/// <summary>
/// A matchday of the championship, told to every manager in the country.
/// </summary>
/// <remarks>
/// <para>
/// It is the one message that is the same fact for everybody and different in the first line for
/// each reader, which is the whole reason it exists. Thirty-two results and four tables are the
/// country's afternoon; what any single manager needs from it is his own result, what it did to
/// his own place, and enough of the rest to know whether the shape of his season is changing.
/// </para>
///
/// <para>
/// It is not a season summary repeated thirty-four times, and the difference is what it leaves
/// out. A season summary exists once, in January, and can afford sixteen lines a table; a
/// matchday arrives thirty-four times a season, and a box that printed every line of every table
/// every Wednesday would be a box a manager learned to swipe past by Wednesday of the second
/// month.
/// </para>
/// </remarks>
public class RoundSummaryFacts
{
    public required Guid SeasonId { get; init; }
    public required string SeasonName { get; init; }

    /// <summary>The championship round, counted from one.</summary>
    public required int RoundNumber { get; init; }

    /// <summary>The calendar day the round was played on, when the season's calendar says one.</summary>
    public int? MatchDayNumber { get; init; }

    /// <summary>The divisions' tables after the round, top first.</summary>
    public required IReadOnlyList<RoundSummaryDivision> Divisions { get; init; }

    /// <summary>Every result of the day, including the competitions that keep no table.</summary>
    public required IReadOnlyList<RoundSummaryFixture> Fixtures { get; init; }
}
