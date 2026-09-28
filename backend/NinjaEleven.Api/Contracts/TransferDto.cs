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
    public Guid SellingClubId { get; init; }
    public string SellingClubName { get; init; } = string.Empty;
    public Guid BuyingClubId { get; init; }
    public string BuyingClubName { get; init; } = string.Empty;
    public int ProposalSeasonNumber { get; init; }
    public int ArrivalSeasonNumber { get; init; }
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
    public double Stars { get; init; }

    public Guid? TeamId { get; init; }
    public string? TeamName { get; init; }
    public string? TeamPrimaryColor { get; init; }
    public string? TeamSecondaryColor { get; init; }

    public int Energy { get; init; }
    public string Injury { get; init; } = string.Empty;
    public int InjuryMatchesRemaining { get; init; }
    public bool Retiring { get; init; }

    public decimal? MarketValue { get; init; }
    public decimal? Salary { get; init; }
    public int ContractSeasons { get; init; }
    public int SeasonsLeft { get; init; }
    public bool IsInLastSeason { get; init; }
    public decimal? AskingPrice { get; init; }

    public PlayerCareerLineDto Season { get; init; } = new();
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

public class TransferSearchResultDto
{
    public IReadOnlyList<TransferListingDto> Players { get; init; } = Array.Empty<TransferListingDto>();
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}

public class TransferHistoryLineDto
{
    public Guid PlayerId { get; init; }
    public Guid SellingClubId { get; init; }
    public string SellingClubName { get; init; } = string.Empty;
    public Guid BuyingClubId { get; init; }
    public string BuyingClubName { get; init; } = string.Empty;
    public decimal Fee { get; init; }
    public TransferStatus Status { get; init; }
    public DateOnly ProposedAt { get; init; }
    public DateOnly? ResolvedAt { get; init; }
    public DateOnly? CompletedAt { get; init; }
    public int ProposalSeasonNumber { get; init; }
    public int ArrivalSeasonNumber { get; init; }
}

public class NpcTransferResultDto
{
    public int ProposalsMade { get; init; }
    public int Accepted { get; init; }
    public int Rejected { get; init; }
    public int Completed { get; init; }
}

public class ReleaseResultDto
{
    public Guid PlayerId { get; init; }
    public string PlayerName { get; init; } = string.Empty;
    public Guid ClubId { get; init; }
    public string ClubName { get; init; } = string.Empty;
    public decimal ReleaseCost { get; init; }
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
}

public class TransferRetireRequestDto
{
    public bool Retiring { get; init; }
}

public class TransferCompleteRequestDto
{
    public Guid ArrivalSeasonId { get; init; }
    public int ArrivalRound { get; init; }
}
