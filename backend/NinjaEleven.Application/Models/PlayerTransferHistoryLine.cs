using NinjaEleven.Domain.Transfers;

namespace NinjaEleven.Application.Models;

/// <summary>
/// One line in a player's transfer history: a club, a fee, and the outcome of the deal.
///
/// It is read from the <see cref="Transfer"/> record and nothing else. A player's moves
/// are his own story, and a screen that reassembled it from two sources would be a story
/// with two tellings.
/// </summary>
public class PlayerTransferHistoryLine
{
    public Guid PlayerId { get; set; }

    /// <summary>
    /// The club he left, or null when nobody sold him — a player with no club who was signed has
    /// a history line too, and a history that skipped those lines would be missing every move of
    /// a career that began without one.
    /// </summary>
    public Guid? SellingClubId { get; set; }
    public string SellingClubName { get; set; } = string.Empty;

    public Guid BuyingClubId { get; set; }
    public string BuyingClubName { get; set; } = string.Empty;

    /// <summary>The fee agreed between the two clubs, in limos.</summary>
    public decimal Fee { get; set; }

    public TransferStatus Status { get; set; }

    public DateOnly ProposedAt { get; set; }
    public DateOnly? ResolvedAt { get; set; }
    public DateOnly? CompletedAt { get; set; }

    /// <summary>The number of the season the proposal was made in.</summary>
    public int ProposalSeasonNumber { get; set; }

    /// <summary>The number of the season the player was due to arrive in.</summary>
    public int ArrivalSeasonNumber { get; set; }
}
