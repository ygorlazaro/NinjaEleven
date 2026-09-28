using NinjaEleven.Domain.Managers;
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

    /// <summary>
    /// Whether this is the club the manager is running, which is a fact the world needs and not
    /// only the screen does.
    ///
    /// Almost everything in the game treats all thirty-six clubs the same, and that is right:
    /// the engine plays the club nobody is watching. Two things are not the same, though, and
    /// both of them are about a club's own decisions rather than its football. A club sells a
    /// player, and a club that is being played answers for itself — the offer goes to a manager
    /// to accept or refuse rather than being settled by a roll of the dice on his behalf. So
    /// the one club a manager has picked is marked, and the market is the only thing in the
    /// game that reads the mark.
    /// </summary>
    public bool IsManagerClub { get; private set; }

    /// <summary>The manager row for this club, if one has been created. One club has at most one manager.</summary>
    public ICollection<Manager> Managers { get; private set; } = new List<Manager>();

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
            IsManagerClub = false
        };
    }

    /// <summary>Marks this club as the one a manager is running.</summary>
    public void MarkAsManagerClub() => IsManagerClub = true;

    /// <summary>Stops this club being the one a manager is running.</summary>
    public void ClearManagerClub() => IsManagerClub = false;

    /// <summary>Changes the club's display name.</summary>
    public void SetName(string name)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("A club needs a name.", nameof(name))
            : name.Trim();
    }

    /// <summary>Changes the club's primary kit colour.</summary>
    public void SetPrimaryColor(string color)
    {
        if (string.IsNullOrWhiteSpace(color))
            throw new ArgumentException("A club needs a primary colour.", nameof(color));
        PrimaryColor = color.Trim();
    }

    /// <summary>Changes the club's secondary kit colour.</summary>
    public void SetSecondaryColor(string color)
    {
        if (string.IsNullOrWhiteSpace(color))
            throw new ArgumentException("A club needs a secondary colour.", nameof(color));
        SecondaryColor = color.Trim();
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