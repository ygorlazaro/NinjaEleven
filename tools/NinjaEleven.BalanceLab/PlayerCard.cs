using NinjaEleven.Domain.Enums;

namespace NinjaEleven.BalanceLab;

/// <summary>
/// One player as the laboratory sees him: six numbers of quality, a position, an age and an
/// amount of energy.
///
/// <para>
/// It is a card rather than a <c>Player</c> on purpose. The laboratory asks "what would
/// happen if these two men played", and the entity carries a face, a club, a contract and an
/// identity that a hundred thousand simulated confrontos do not need. Building a squad of
/// these costs four allocations where a <c>Player</c> plus a <c>PlayerSeasonState</c> costs two
/// objects and a <c>Guid.NewGuid()</c> each, and the whole point of the laboratory is to run
/// a hundred thousand of them.
/// </para>
/// </summary>
public sealed record PlayerCard
{
    public required Position Position { get; init; }
    public required int Speed { get; init; }
    public required int Accuracy { get; init; }
    public required int Dribbling { get; init; }
    public required int Heading { get; init; }
    public required int Strength { get; init; }
    public required int Energy { get; init; }
    public int GoalkeeperPower { get; init; }
    public int Reflexes { get; init; }
    public int Age { get; init; } = 26;

    /// <summary>
    /// A hypothetical stamina. It does not exist on <c>Player</c> yet, and it is on the card
    /// so the laboratory can price it before a migration is written for it — the question is
    /// whether it buys anything the energy curve does not already buy.
    /// </summary>
    public int Stamina { get; init; } = 60;

    /// <summary>The six outfield attributes, for the cases where a scenario moves all of them together.</summary>
    public int AverageOutfield => (Speed + Accuracy + Dribbling + Heading + Strength) / 5;

    /// <summary>
    /// An outfield card whose six attributes are all the same number, which is how a
    /// scenario says "a player of this quality" without also saying which kind of player he is.
    /// </summary>
    public static PlayerCard Outfield(Position position, int quality, int energy, int age = 26, int stamina = 60) =>
        new()
        {
            Position = position,
            Speed = quality,
            Accuracy = quality,
            Dribbling = quality,
            Heading = quality,
            Strength = quality,
            Energy = energy,
            Age = age,
            Stamina = stamina
        };

    /// <summary>A keeper of a given quality, where quality is his reflexes.</summary>
    public static PlayerCard Keeper(int quality, int energy, int age = 26, int stamina = 60) =>
        new()
        {
            Position = Position.GK,
            Speed = quality,
            Accuracy = quality,
            Dribbling = quality / 2,
            Heading = quality / 2,
            Strength = quality,
            GoalkeeperPower = quality,
            Reflexes = quality,
            Energy = energy,
            Age = age,
            Stamina = stamina
        };

    /// <summary>The same man, somewhere else on the scale of tiredness.</summary>
    public PlayerCard AtEnergy(int energy) => this with { Energy = energy };

    /// <summary>The same man, with a different amount in the tank than the card was drawn with.</summary>
    public PlayerCard WithEnergy(int energy) => AtEnergy(energy);
}
