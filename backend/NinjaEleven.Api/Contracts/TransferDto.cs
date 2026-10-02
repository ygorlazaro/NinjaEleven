using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Transfers;

namespace NinjaEleven.Api.Contracts;

public class TransferProposalDto
{
    public Guid TransferId { get; init; }
    public Guid PlayerId { get; init; }
    public string PlayerName { get; init; } = string.Empty;
    public string PlayerPosition { get; init; } = string.Empty;
    public int PlayerAge { get; init; }

    /// <summary>Null when the player has no club: a signing has nobody selling him.</summary>
    public Guid? SellingClubId { get; init; }
    public string SellingClubName { get; init; } = string.Empty;

    public Guid BuyingClubId { get; init; }
    public string BuyingClubName { get; init; } = string.Empty;
    public int ProposalSeasonNumber { get; init; }

    /// <summary>The season he arrives in, which is not always the one after this.</summary>
    public int ArrivalSeasonNumber { get; init; }

    /// <summary>The round he walks in on.</summary>
    public int? ArrivalRoundNumber { get; init; }

    public decimal Fee { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateOnly ProposedAt { get; init; }
    public DateOnly? ResolvedAt { get; init; }
    public DateOnly? CompletedAt { get; init; }
}

public class TransferListingDto
{
    public Guid PlayerId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Position { get; init; } = string.Empty;
    public int Age { get; init; }
    public int Speed { get; init; }
    public int Accuracy { get; init; }
    public int Dribbling { get; init; }
    public int Heading { get; init; }
    public int Strength { get; init; }
    public int GoalkeeperPower { get; init; }
    public int Reflexes { get; init; }
    public int Stamina { get; init; }
    public int Potential { get; init; }
    public double Stars { get; init; }

    public Guid? TeamId { get; init; }
    public string? TeamName { get; init; }
    public string? TeamPrimaryColor { get; init; }
    public string? TeamSecondaryColor { get; init; }

    /// <summary>Whether he is a free agent, and so is signed rather than bought.</summary>
    public bool IsFreeAgent { get; init; }

    /// <summary>
    /// Whether somebody already has a live deal on him — a proposal waiting for an answer or one
    /// already agreed. The market refuses a second offer on a man who is spoken for, so the row
    /// has to say so: a manager who is only told by an error code that his own proposal from two
    /// minutes ago is still on the table has been told something the market knew all along.
    /// </summary>
    public bool HasActiveProposal { get; init; }

    public int Energy { get; init; }
    public string Injury { get; init; } = string.Empty;
    public int InjuryMatchesRemaining { get; init; }
    public bool Retiring { get; init; }

    /// <summary>
    /// Whether the manager has placed this player on the active transfer list.
    /// Transfer-listed players appear on the market alongside free agents.
    /// </summary>
    public bool OnTransferList { get; init; }

    public decimal? MarketValue { get; init; }
    public decimal? Salary { get; init; }
    public int ContractSeasons { get; init; }
    public int SeasonsLeft { get; init; }
    public bool IsInLastSeason { get; init; }
    public decimal? AskingPrice { get; init; }

    public PlayerCareerLineDto Season { get; init; } = new();

    /// <summary>The whole career, every club included.</summary>
    public PlayerCareerLineDto Total { get; init; } = new();

    /// <summary>The career split by club, on the card that opens from a row.</summary>
    public IReadOnlyList<PlayerClubCareerLineDto> Clubs { get; init; } = Array.Empty<PlayerClubCareerLineDto>();
}

/// <summary>
/// One club's share of a career, and the seasons the man spent in that shirt.
/// </summary>
public class PlayerClubCareerLineDto
{
    public Guid TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public int Seasons { get; init; }
    public PlayerCareerLineDto Total { get; init; } = new();
}

public class TransferInboxDto
{
    public Guid ClubId { get; init; }
    public string ClubName { get; init; } = string.Empty;
    public int ProposalSeasonNumber { get; init; }
    public IReadOnlyList<TransferProposalDto> Incoming { get; init; } = Array.Empty<TransferProposalDto>();
    public IReadOnlyList<TransferProposalDto> Outgoing { get; init; } = Array.Empty<TransferProposalDto>();
}

/// <summary>
/// Everything the market was narrowed by, in one query string. Anything left out is not a
/// filter, so a manager who sets two of them gets the list narrowed by exactly those two.
/// </summary>
public class TransferSearchQuery
{
    public Guid SeasonId { get; init; }
    public Position? Position { get; init; }
    public int? MinAge { get; init; }
    public int? MaxAge { get; init; }
    public double? MinStars { get; init; }
    public double? MaxStars { get; init; }
    public int? MinSpeed { get; init; }
    public int? MinAccuracy { get; init; }
    public int? MinDribbling { get; init; }
    public int? MinHeading { get; init; }
    public int? MinStrength { get; init; }
    public int? MinGoalkeeperPower { get; init; }
    public int? MinReflexes { get; init; }
    public bool? Retiring { get; init; }
    public bool FreeAgentsOnly { get; init; }
    public bool WithClubOnly { get; init; }
    public Guid? TeamId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 30;
}

public class TransferWindowStateDto
{
    public int SeasonNumber { get; init; }
    public int CurrentRound { get; init; }
    public bool IsOpen { get; init; }
    public int ArrivalSeasonNumber { get; init; }
    public int ArrivalRoundNumber { get; init; }

    /// <summary>The same two facts in a sentence, in a manager's words.</summary>
    public string ArrivalLabel { get; init; } = string.Empty;
}

public class TransferSearchResultDto
{
    public IReadOnlyList<TransferListingDto> Players { get; init; } = Array.Empty<TransferListingDto>();
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
    public TransferWindowStateDto Window { get; init; } = new();
}

public class TransferHistoryLineDto
{
    public Guid TransferId { get; init; }
    public Guid PlayerId { get; init; }
    public string PlayerName { get; init; } = string.Empty;
    public string PlayerPosition { get; init; } = string.Empty;

    public Guid? SellingClubId { get; init; }
    public string? SellingClubName { get; init; }

    public Guid BuyingClubId { get; init; }
    public string BuyingClubName { get; init; } = string.Empty;

    public int ProposalSeasonNumber { get; init; }
    public int ArrivalSeasonNumber { get; init; }
    public int? ArrivalRoundNumber { get; init; }

    public decimal Fee { get; init; }
    public TransferStatus Status { get; init; }
    public DateOnly ProposedAt { get; init; }
    public DateOnly? ResolvedAt { get; init; }
    public DateOnly? CompletedAt { get; init; }
}

/// <summary>
/// The recent business of a division: the transfers that finished in the last few rounds,
/// across every club in it. A market that shows what happened recently is a market a manager
/// can read without opening a second screen.
/// </summary>
public class DivisionRecentTransfersDto
{
    public Guid CompetitionSeasonId { get; init; }
    public int CurrentRound { get; init; }
    public int WindowRounds { get; init; }
    public IReadOnlyList<TransferHistoryLineDto> Transfers { get; init; } = Array.Empty<TransferHistoryLineDto>();
}

/// <summary>
/// A club's transfer history: every deal the club was involved in, across the seasons asked
/// for, newest first. Pending and accepted sit in the same table as completed ones, because a
/// proposal is a fact about the club's season whether or not the selling club has answered it.
/// </summary>
public class ClubTransferHistoryDto
{
    public Guid TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public IReadOnlyList<int> SeasonNumbers { get; init; } = Array.Empty<int>();
    public IReadOnlyList<TransferHistoryLineDto> Transfers { get; init; } = Array.Empty<TransferHistoryLineDto>();
}

/// <summary>
/// A club's balance, as the market reads it: the money the club has, and nothing else. It is
/// read from the last line written in the club's whole book, so a balance that followed a
/// filter would tell a manager his club had as much as it had spent, which is the one number
/// in the game that would be plainly wrong.
/// </summary>
public class ClubBalanceDto
{
    public Guid TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public decimal Balance { get; init; }
}

public class NpcTransferResultDto
{
    public int ProposalsMade { get; init; }
    public int Accepted { get; init; }
    public int Rejected { get; init; }
    public int Signed { get; init; }
    public int Completed { get; init; }

    /// <summary>How many offers on the table were answered this round.</summary>
    public int Answered { get; init; }
}

public class ReleaseResultDto
{
    public Guid PlayerId { get; init; }
    public string PlayerName { get; init; } = string.Empty;
    public Guid ClubId { get; init; }
    public string ClubName { get; init; } = string.Empty;
    public decimal ReleaseCost { get; init; }

    /// <summary>
    /// The offers the club had on the table for this player, which the release withdrew. A club
    /// that has let a man go has nothing left to sell, and an offer still naming him as one of
    /// its players is a promise it cannot keep.
    /// </summary>
    public int WithdrawnOffers { get; init; }

    public string Message { get; init; } = string.Empty;
}

public class TransferProposeRequestDto
{
    public Guid PlayerId { get; init; }
    public Guid BuyingClubId { get; init; }
    public decimal? Fee { get; init; }
}

public class TransferAnswerRequestDto
{
    public bool Accept { get; init; }

    /// <summary>
    /// The club answering. It has to be the selling club, and it is named in the request
    /// because the check is a real one: a buying club that could accept its own offer would be
    /// setting its own price and signing its own cheque.
    /// </summary>
    public Guid ClubId { get; init; }
}

public class TransferRankingsDto
{
    public Guid CompetitionSeasonId { get; init; }
    public IReadOnlyList<TransferRankingEntryDto> MostBought { get; init; } = Array.Empty<TransferRankingEntryDto>();
    public IReadOnlyList<TransferRankingEntryDto> MostSold { get; init; } = Array.Empty<TransferRankingEntryDto>();
    public IReadOnlyList<TransferRankingEntryDto> MostSpent { get; init; } = Array.Empty<TransferRankingEntryDto>();
    public IReadOnlyList<TransferRankingEntryDto> MostProfit { get; init; } = Array.Empty<TransferRankingEntryDto>();
}

public class TransferRankingEntryDto
{
    public Guid TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public string TeamShortName { get; init; } = string.Empty;
    public string PrimaryColor { get; init; } = string.Empty;
    public string SecondaryColor { get; init; } = string.Empty;
    public int Transfers { get; init; }
    public decimal Amount { get; init; }
}

