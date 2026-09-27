namespace NinjaEleven.Domain.Teams;

/// <summary>
/// Represents a club's stadium. Capacity is fixed at 5000 per spec.
/// Ticket price is fixed at 10 limos per spec.
/// </summary>
public class Stadium
{
    public Guid Id { get; private set; }
    public Guid ClubId { get; private set; }
    public int Capacity { get; private set; }
    public decimal TicketPrice { get; private set; }

    private Stadium() { }

    public static Stadium Create(Guid clubId)
    {
        return new Stadium
        {
            Id = Guid.NewGuid(),
            ClubId = clubId,
            Capacity = 5000,
            TicketPrice = 10m
        };
    }
}