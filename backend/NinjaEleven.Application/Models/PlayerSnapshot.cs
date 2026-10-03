using NinjaEleven.Domain.Players;

namespace NinjaEleven.Application.Models;

/// <summary>
/// What a box about a man is made of: his face, the seven attributes the engine scores him on,
/// his stars and how much football he has left.
///
/// <para>
/// It exists because the same eight facts were about to be written out three times over — once
/// on the squad, once on the market listing, once on each of the two transfer feeds — and a
/// fourth copy is a fourth thing to forget when an attribute is added. A list that repeats
/// itself is a list whose copies drift apart, and a market where the proposal tab prices a man
/// on three attributes the listing tab prices him on five is a market quoting two different
/// players under one name.
/// </para>
///
/// <para>
/// Energy travels with it because it is the one fact here that belongs to a season rather than
/// to the man, and so it is the one that can be absent: a transfer of somebody nobody has a
/// season state for is a real transfer of a real man with no energy to report, and that is a
/// null and not a hundred.
/// </para>
/// </summary>
public class PlayerSnapshot
{
    public string? Face { get; init; }
    public int Speed { get; init; }
    public int Accuracy { get; init; }
    public int Dribbling { get; init; }
    public int Heading { get; init; }
    public int Strength { get; init; }
    public int GoalkeeperPower { get; init; }
    public int Reflexes { get; init; }
    public double Stars { get; init; }

    /// <summary>Null when the world has no season state for him, which is not the same as zero.</summary>
    public int? Energy { get; init; }

    /// <summary>
    /// Reads the eight facts off a player and the one fact off his season state, when there is
    /// one. It never asks for anything: both arguments are entities the caller already holds,
    /// because a snapshot that went and fetched a player would be a question asked per row.
    /// </summary>
    public static PlayerSnapshot Of(Player player, PlayerSeasonState? state) => new()
    {
        Face = player.Face,
        Speed = player.Speed,
        Accuracy = player.Accuracy,
        Dribbling = player.Dribbling,
        Heading = player.Heading,
        Strength = player.Strength,
        GoalkeeperPower = player.GoalkeeperPower,
        Reflexes = player.Reflexes,
        Stars = PlayerRating.CalculateStars(player),
        Energy = state?.Energy
    };
}