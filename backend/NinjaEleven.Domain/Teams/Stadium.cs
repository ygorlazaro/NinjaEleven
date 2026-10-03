namespace NinjaEleven.Domain.Teams;

/// <summary>
/// A club's ground.
///
/// The name is stored rather than composed from the club's name on the way to a screen. A
/// stadium that is called "Vila Nova do Vale Stadium" because that is what the club is called
/// today cannot ever be anything else, and the first thing a manager does with a ground is
/// give it a name of its own — so the name is a column the day the column can be edited, not
/// a string built at the moment it is needed.
///
/// <para>
/// The capacity is the ground's own, and it is not the club's. A club of forty thousand people
/// and a ground of five thousand is an ordinary state of a football club's finances rather than
/// a contradiction, and it is the state that makes an expansion worth doing. So the two numbers
/// live apart and nothing here derives one from the other.
/// </para>
/// </summary>
public class Stadium
{
    public Guid Id { get; private set; }
    public Guid ClubId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Capacity { get; private set; }
    public decimal TicketPrice { get; private set; }

    private Stadium() { }

    /// <summary>Capacity a new ground is built to.</summary>
    public const int DefaultCapacity = 5000;

    /// <summary>What a seat costs when the game opens.</summary>
    public const decimal DefaultTicketPrice = 10m;

    public static Stadium Create(Guid clubId, string clubName)
    {
        if (string.IsNullOrWhiteSpace(clubName))
        {
            throw new ArgumentException(
                "A stadium is named after its club, so it needs a club to be named after.",
                nameof(clubName));
        }

        return new Stadium
        {
            Id = Guid.NewGuid(),
            ClubId = clubId,
            Name = DefaultName(clubName),
            Capacity = DefaultCapacity,
            TicketPrice = DefaultTicketPrice
        };
    }

    /// <summary>The name a ground gets the day it is built.</summary>
    public static string DefaultName(string clubName) => $"{clubName.Trim()} Stadium";

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A stadium needs a name.", nameof(name));
        }

        Name = name.Trim();
    }

    /// <summary>Resize the ground. A ground of zero seats is a car park.</summary>
    public void SetCapacity(int capacity)
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity), capacity, "A stadium cannot have fewer than no seats.");
        }

        Capacity = capacity;
    }

    public void SetTicketPrice(decimal ticketPrice)
    {
        if (ticketPrice < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ticketPrice), ticketPrice, "A ticket cannot cost less than nothing.");
        }

        TicketPrice = ticketPrice;
    }

    /// <summary>
    /// Opens the seats at the price the game decides, rather than at a price somebody typed.
    ///
    /// <para>
    /// It is a decision the domain makes and the club records, in that order: the club asks what
    /// a seat is worth to its own people, the rule answers, and the answer is stored like any
    /// other fact about the ground. A screen that offered a price field would be offering to
    /// invent the one number on the club's page that the game has an opinion about.
    /// </para>
    /// </summary>
    /// <param name="crowd">How many people the club plays to, before a price is known.</param>
    public void PriceTheSeatsFor(int crowd)
    {
        SetTicketPrice(StadiumRules.TicketPriceFor(Capacity, crowd));
    }
}
