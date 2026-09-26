namespace FootballManager.Domain.Common;

/// <summary>
/// Deterministic RNG used by tests and by the Match Engine when a seed is supplied.
/// Uses a 32-bit xorshift generator so the same seed always produces the same sequence.
/// </summary>
public sealed class DeterministicRandomSource : IRandomSource
{
    private uint _state;

    public DeterministicRandomSource(int seed)
    {
        _state = (uint)seed;
    }

    public int Next(int min, int max)
    {
        if (min > max)
            throw new ArgumentOutOfRangeException(nameof(max), "min must be <= max.");
        return min + (int)(NextUInt() % (uint)(max - min + 1));
    }

    public double NextDouble()
    {
        return NextUInt() / (double)uint.MaxValue;
    }

    private uint NextUInt()
    {
        uint x = _state;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _state = x;
        return x;
    }
}