using NinjaEleven.Domain.Sponsors;

namespace NinjaEleven.Domain.Teams;

/// <summary>
/// Represents a football club. The squad is NOT stored as a list inside Team;
/// the link between player and club is expressed through TeamMembership.
/// </summary>
public class Team
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string ShortName { get; private set; } = string.Empty;
    public string PrimaryColor { get; private set; } = string.Empty;
    public string SecondaryColor { get; private set; } = string.Empty;
    public int Rating { get; private set; }
    public Guid? StadiumId { get; private set; }
    public Stadium? Stadium { get; private set; }

    /// <summary>The current active sponsor contract on this club's shirt, if any.</summary>
    public Guid? ActiveSponsorContractId { get; private set; }
    public SponsorContract? ActiveSponsorContract { get; private set; }

    private Team() { }

    public static Team Create(
        string name,
        string shortName,
        string primaryColor,
        string secondaryColor,
        int rating = 50)
    {
        return new Team
        {
            Id = Guid.NewGuid(),
            Name = name ?? throw new ArgumentNullException(nameof(name)),
            ShortName = string.IsNullOrEmpty(shortName) ? name : shortName,
            PrimaryColor = string.IsNullOrEmpty(primaryColor) ? "#3a6ea5" : primaryColor,
            SecondaryColor = string.IsNullOrEmpty(secondaryColor) ? "#1f3c56" : secondaryColor,
            Rating = Math.Max(1, Math.Min(100, rating)),
        };
    }

    public void SetStadium(Stadium stadium)
    {
        Stadium = stadium;
        StadiumId = stadium?.Id;
    }

    /// <summary>
    /// Signs a sponsor deal for this club. A club can only sign one deal at a time: the
    /// current one must be paid off before another can take its place.
    /// </summary>
    public void SignSponsorContract(SponsorContract contract)
    {
        if (ActiveSponsorContract is not null && ActiveSponsorContract.IsActive)
            throw new InvalidOperationException("A club cannot have two active sponsor deals.");

        ActiveSponsorContract = contract;
        ActiveSponsorContractId = contract.Id;
    }

    /// <summary>Clears the current sponsor deal without recording it as paid or terminated.</summary>
    public void ClearSponsorContract()
    {
        ActiveSponsorContract = null;
        ActiveSponsorContractId = null;
    }
}