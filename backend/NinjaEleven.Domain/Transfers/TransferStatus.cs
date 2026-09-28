namespace NinjaEleven.Domain.Transfers;

/// <summary>
/// Where a transfer proposal is in its life cycle.
/// </summary>
public enum TransferStatus
{
    /// <summary>A proposal has been made and is waiting on the selling club.</summary>
    Pending,

    /// <summary>The selling club accepted. The player moves after the window opens.</summary>
    Accepted,

    /// <summary>The selling club refused. The proposal is dead.</summary>
    Rejected,

    /// <summary>The player has arrived at his new club and the deal is complete.</summary>
    Completed,

    /// <summary>The proposal expired without an answer. It is dead like a rejection.</summary>
    Expired
}