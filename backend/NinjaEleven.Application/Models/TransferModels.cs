using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Players;

namespace NinjaEleven.Application.Models;

/// <summary>
/// A transfer proposal as a manager reads it in his inbox.
/// </summary>
public class TransferProposal
{
    public Guid TransferId { get; set; }
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string PlayerPosition { get; set; } = string.Empty;
    public int PlayerAge { get; set; }

    /// <summary>
    /// The club selling the player, or null when he has none and is being signed. An offer for
    /// a free agent is not a purchase and the screen must not draw it as one.
    /// </summary>
    public Guid? SellingClubId { get; set; }
    public string SellingClubName { get; set; } = string.Empty;

    /// <summary>The club that made the proposal.</summary>
    public Guid BuyingClubId { get; set; }
    public string BuyingClubName { get; set; } = string.Empty;

    public int ProposalSeasonNumber { get; set; }

    /// <summary>The season the player arrives in, which is not always the one after this.</summary>
    public int ArrivalSeasonNumber { get; set; }

    /// <summary>
    /// The round he walks in on: the eleventh of this season for a mid-season window, the first
    /// of the next for one that waits for the Supercup.
    /// </summary>
    public int? ArrivalRoundNumber { get; set; }

    public decimal Fee { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateOnly ProposedAt { get; set; }
    public DateOnly? ResolvedAt { get; set; }
    public DateOnly? CompletedAt { get; set; }
}

/// <summary>
/// A player as the transfer market reads him: who he is, what he is worth, and whether he
/// is on a club's books at all. This is the row the transfer screen shows.
/// </summary>
public class TransferListing
{
    public Guid PlayerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public int Age { get; set; }

    public int Speed { get; set; }
    public int Accuracy { get; set; }
    public int Dribbling { get; set; }
    public int Heading { get; set; }
    public int Strength { get; set; }
    public int GoalkeeperPower { get; set; }
    public int Reflexes { get; set; }

    public double Stars { get; set; }

    /// <summary>Null when the player is not on a club this season.</summary>
    public Guid? TeamId { get; set; }
    public string? TeamName { get; set; }
    public string? TeamPrimaryColor { get; set; }
    public string? TeamSecondaryColor { get; set; }

    /// <summary>
    /// Whether he is a free agent: nobody to buy him from, nobody to ask, and a club that wants
    /// him signs him rather than bidding. The screen needs the flag as much as the club's name,
    /// because a man with no club is a different kind of row from a man with one.
    /// </summary>
    public bool IsFreeAgent { get; set; }

    /// <summary>
    /// Whether somebody already has a live deal on him: a proposal waiting for an answer or one
    /// already agreed. The market refuses a second offer on a man who is spoken for
    /// (<c>TransferInProgress</c>), so the row says it up front — including the manager's own
    /// proposal, which is the one he is most likely to make twice and least likely to remember
    /// making once.
    /// </summary>
    public bool HasActiveProposal { get; set; }

    public int Energy { get; set; }
    public string Injury { get; set; } = string.Empty;
    public int InjuryMatchesRemaining { get; set; }

    /// <summary>
    /// What he is worth and what he costs, both null only when the market has no season state
    /// to read. A free agent has both: his price is his worth, and the only thing missing is
    /// somebody willing to pay it.
    /// </summary>
    public decimal? MarketValue { get; set; }
    public decimal? Salary { get; set; }
    public int ContractSeasons { get; set; }
    public int SeasonsLeft { get; set; }
    public bool IsInLastSeason { get; set; }

    /// <summary>What a club has to pay to take him from his current one; null for a free agent.</summary>
    public decimal? AskingPrice { get; set; }

    /// <summary>Whether the player has said he will retire at the end of the season.</summary>
    public bool Retiring { get; set; }

    /// <summary>
    /// The season's own totals, so the market shows the same numbers the squad table shows
    /// rather than a second set the manager has to reconcile.
    /// </summary>
    public PlayerCareerLine Season { get; set; } = new();

    /// <summary>The whole career, every club included, summed from his match lines.</summary>
    public PlayerCareerLine Total { get; set; } = new();

    /// <summary>
    /// The career split by club, filled in on the card that opens from a row. A total says a man
    /// scored fifty; this says who he scored them for, which is the half a club reads before it
    /// bids on him.
    /// </summary>
    public List<PlayerClubCareerLine> Clubs { get; set; } = new();
}

/// <summary>
/// What a club has been offered and what it has offered, in one answer.
/// </summary>
public class TransferInbox
{
    public Guid ClubId { get; set; }
    public string ClubName { get; set; } = string.Empty;
    public int ProposalSeasonNumber { get; set; }
    public IReadOnlyList<TransferProposal> Incoming { get; set; } = Array.Empty<TransferProposal>();
    public IReadOnlyList<TransferProposal> Outgoing { get; set; } = Array.Empty<TransferProposal>();
}

/// <summary>
/// Everything a manager narrows a market by, in one object, so the filters travel together and
/// the answer is the whole list narrowed rather than a different list per box.
///
/// A null is a filter nobody set, and only a set filter excludes anybody. That is the whole
/// contract: a market that loaded a different set of men for each combination of boxes is a
/// market that disagrees with itself, and a manager cannot tell a filter from a bug.
/// </summary>
public class TransferSearchFilters
{
    public Position? Position { get; set; }

    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }

    public double? MinStars { get; set; }
    public double? MaxStars { get; set; }

    public int? MinSpeed { get; set; }
    public int? MinAccuracy { get; set; }
    public int? MinDribbling { get; set; }
    public int? MinHeading { get; set; }
    public int? MinStrength { get; set; }
    public int? MinGoalkeeperPower { get; set; }
    public int? MinReflexes { get; set; }

    /// <summary>Only the men who have said they are retiring, or only the men who have not.</summary>
    public bool? Retiring { get; set; }

    /// <summary>Only the players with no club: the ones a club signs rather than buys.</summary>
    public bool FreeAgentsOnly { get; set; }

    /// <summary>The other half of the same question: only the men somebody is paying.</summary>
    public bool WithClubOnly { get; set; }

    /// <summary>One club's squad, for a manager looking at a rival before he bids.</summary>
    public Guid? TeamId { get; set; }

    /// <summary>Whether the player himself answers every filter set. He knows nothing of seasons.</summary>
    public bool Matches(Player player)
    {
        if (Position.HasValue && player.Position != Position.Value) return false;
        if (MinAge.HasValue && player.Age < MinAge.Value) return false;
        if (MaxAge.HasValue && player.Age > MaxAge.Value) return false;

        var stars = PlayerRating.CalculateStars(player);
        if (MinStars.HasValue && stars < MinStars.Value) return false;
        if (MaxStars.HasValue && stars > MaxStars.Value) return false;

        if (MinSpeed.HasValue && player.Speed < MinSpeed.Value) return false;
        if (MinAccuracy.HasValue && player.Accuracy < MinAccuracy.Value) return false;
        if (MinDribbling.HasValue && player.Dribbling < MinDribbling.Value) return false;
        if (MinHeading.HasValue && player.Heading < MinHeading.Value) return false;
        if (MinStrength.HasValue && player.Strength < MinStrength.Value) return false;
        if (MinGoalkeeperPower.HasValue && player.GoalkeeperPower < MinGoalkeeperPower.Value) return false;
        if (MinReflexes.HasValue && player.Reflexes < MinReflexes.Value) return false;

        return true;
    }

    /// <summary>Whether the season he is in this season answers the filters that need one.</summary>
    public bool MatchesTheState(PlayerSeasonState? state)
    {
        if (Retiring.HasValue && (state?.Retiring ?? false) != Retiring.Value) return false;

        var contracted = state?.TeamId is not null;
        if (FreeAgentsOnly && contracted) return false;
        if (WithClubOnly && !contracted) return false;
        if (TeamId.HasValue && state?.TeamId != TeamId.Value) return false;

        return true;
    }
}

/// <summary>
/// Where the world is in its transfer calendar, as a manager reads it.
///
/// The screen is told which round is being played, whether a man bought now would move this
/// season, and the round he would move on. It does not work that out: a number a manager reads
/// is a number the rules decided, and a screen that counted the rounds itself would eventually
/// count them differently from the calendar they are played on.
/// </summary>
public class TransferWindowState
{
    public int SeasonNumber { get; set; }

    /// <summary>The championship round the world is in.</summary>
    public int CurrentRound { get; set; }

    /// <summary>Whether the mid-season window is open right now.</summary>
    public bool IsOpen { get; set; }

    public int ArrivalSeasonNumber { get; set; }
    public int ArrivalRoundNumber { get; set; }

    /// <summary>The same two facts in a sentence, in the words a manager would use.</summary>
    public string ArrivalLabel { get; set; } = string.Empty;
}

/// <summary>
/// The result of asking the market for players.
/// </summary>
public class TransferSearchResult
{
    public IReadOnlyList<TransferListing> Players { get; set; } = Array.Empty<TransferListing>();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }

    /// <summary>Where the window is, so the screen can say when a man would arrive.</summary>
    public TransferWindowState Window { get; set; } = new();
}

/// <summary>
/// A club's end-of-round transfer business: what it put on the table, what the market made of
/// it, and how many free agents it picked up while short of the minimum.
/// </summary>
public class NpcTransferRoundResult
{
    public int ProposalsMade { get; set; }
    public int Accepted { get; set; }
    public int Rejected { get; set; }
    public int Signed { get; set; }

    /// <summary>
    /// How many offers that were already on the table were decided this round, accepted or
    /// refused — the ones a manager made for a player one of these clubs holds. It is counted
    /// apart from the proposals the run made itself, because they are two different things: a
    /// club going shopping, and a club deciding whether to sell.
    /// </summary>
    public int Answered { get; set; }
}
