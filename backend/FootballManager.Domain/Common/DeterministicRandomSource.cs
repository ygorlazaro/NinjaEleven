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

    /// <summary>
    /// Same contract as <see cref="System.Random.Next(int, int)"/>: the upper bound is
    /// exclusive. The engine relies on it to index a list with <c>Next(0, count)</c>,
    /// so an inclusive bound here would reach past the end of that list.
    /// </summary>
    public int Next(int min, int max)
    {
        if (min > max)
            throw new ArgumentOutOfRangeException(nameof(max), "min must be <= max.");

        if (min == max)
            return min;

        // Computed as long because the full int range does not fit in an int.
        var range = (long)max - min;
        return (int)(min + (long)(NextUInt() % (ulong)range));
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