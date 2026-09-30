namespace NinjaEleven.BalanceLab;

/// <summary>
/// A random source for the laboratory, seeded and reproducible but not the engine's.
///
/// <para>
/// It is a different generator from <c>DeterministicRandomSource</c> because a laboratory
/// run is a hundred thousand confrontos and xorshift at that volume is a measurable part of
/// the runtime. The important property is the same one the engine's has: a seed replays a run,
/// so a number in a report can be regenerated and the number in the next report is comparable
/// to it.
/// </para>
/// </summary>
public sealed class LabRandom
{
    private ulong _state;

    public LabRandom(int seed)
    {
        _state = (ulong)seed * 6364136223846793005UL + 1442695040888963407UL;
    }

    /// <summary>A double in [0, 1).</summary>
    public double NextDouble()
    {
        _state ^= _state << 13;
        _state ^= _state >> 7;
        _state ^= _state << 17;

        return (_state >> 11) * (1.0 / 9007199254740992.0);
    }
}

/// <summary>
/// The answer to one scenario: how often each side came away with what it came for.
///
/// <para>
/// A confrontation has three outcomes rather than two, and the third one is not a rounding
/// error. A striker who is beaten, or who beats his man and is saved, has not lost a duel —
/// he has had a chance and not converted it, and in a match decided on a single chance those
/// are the same evening. Reporting only wins would make a scenario where both men are poor
/// look like a draw, when it is actually two empty matches.
/// </para>
public sealed record DuelOutcome(int Wins, int Losses, int Draws)
{
    public int Duels => Wins + Losses + Draws;

    public double WinRate => Duels == 0 ? 0 : (double)Wins / Duels;

    public double LossRate => Duels == 0 ? 0 : (double)Losses / Duels;

    public double DrawRate => Duels == 0 ? 0 : (double)Draws / Duels;
}
