using NinjaEleven.Domain.Common;

namespace NinjaEleven.Domain.Sponsors;

/// <summary>
/// A shirt deal: a sponsor pays a club a fee per match for a fixed number of matches.
///
/// The contract is a deal between a sponsor and a club for a season. The sponsor pays a
/// flat fee for each match the club plays under the deal, and the club's only constraint
/// is that it cannot change sponsor until the current deal is paid for — the matches
/// have been sold and the name on the shirt for them has already been bought.
/// </summary>
public class SponsorContract
{
    public Guid Id { get; private set; }

    /// <summary>Which sponsor's name is on the shirt.</summary>
    public Guid SponsorId { get; private set; }
    public Sponsor? Sponsor { get; private set; }

    /// <summary>The club that signed the deal.</summary>
    public Guid TeamId { get; private set; }

    /// <summary>
    /// The season this deal was signed in, which is the season it is paid out of: a deal
    /// signed in the last matchday of one season is paid in the next, and a club carrying
    /// a deal across the off-season keeps its name but its books do.
    /// </summary>
    public Guid SeasonId { get; private set; }

    /// <summary>How much the sponsor pays for each match, in limos.</summary>
    public decimal PerMatchFee { get; private set; }

    /// <summary>How many matches the deal was signed for.</summary>
    public int ContractMatches { get; private set; }

    /// <summary>How many of those matches have been played and paid for.</summary>
    public int MatchesPlayed { get; private set; }

    /// <summary>The state of the deal: active, expired, or terminated.</summary>
    public SponsorContractStatus Status { get; private set; }

    /// <summary>
    /// When the deal was signed, for the ledger description and for ordering a club's
    /// history of shirts.
    /// </summary>
    public DateTimeOffset SignedAt { get; private set; }

    private SponsorContract() { }

    public static SponsorContract Sign(
        Guid sponsorId,
        Guid teamId,
        Guid seasonId,
        decimal perMatchFee,
        int contractMatches)
    {
        if (sponsorId == Guid.Empty)
            throw new ArgumentException("A contract needs a sponsor.", nameof(sponsorId));
        if (teamId == Guid.Empty)
            throw new ArgumentException("A contract needs a club.", nameof(teamId));
        if (seasonId == Guid.Empty)
            throw new ArgumentException("A contract belongs to a season.", nameof(seasonId));
        if (perMatchFee < 0m)
            throw new ArgumentOutOfRangeException(nameof(perMatchFee), perMatchFee, "A sponsor does not pay to take a shirt.");
        if (contractMatches < 1)
            throw new ArgumentOutOfRangeException(nameof(contractMatches), contractMatches, "A contract is for at least one match.");

        return new SponsorContract
        {
            Id = Guid.NewGuid(),
            SponsorId = sponsorId,
            TeamId = teamId,
            SeasonId = seasonId,
            PerMatchFee = perMatchFee,
            ContractMatches = contractMatches,
            MatchesPlayed = 0,
            Status = SponsorContractStatus.Active,
            SignedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// The matches still owed on this deal. Zero is the only moment a club can change sponsor:
    /// the remaining matches have been paid for and the name is theirs for the asking.
    /// </summary>
    public int MatchesLeft => Math.Max(0, ContractMatches - MatchesPlayed);

    /// <summary>Whether the deal is still being paid.</summary>
    public bool IsActive => Status is SponsorContractStatus.Active && MatchesLeft > 0;

    /// <summary>
    /// Records one match played under this deal and expires it when the last is paid for.
    /// Returns true when this call expired the deal.
    /// </summary>
    public bool RecordMatchPlayed()
    {
        if (Status is not SponsorContractStatus.Active)
            return false;

        MatchesPlayed++;

        if (MatchesPlayed >= ContractMatches)
        {
            Status = SponsorContractStatus.Expired;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Ends the deal early, before its matches have run out. Reserved for future use.
    /// </summary>
    public void Terminate()
    {
        if (Status is SponsorContractStatus.Active)
            Status = SponsorContractStatus.Terminated;
    }
}
