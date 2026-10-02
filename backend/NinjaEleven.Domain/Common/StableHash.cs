namespace NinjaEleven.Domain.Common;

/// <summary>
/// A hash of a name that is the same on every machine, in every process, forever.
///
/// <para>
/// FNV-1a, and it is here rather than inline because the world already draws two things from a
/// name — a club's badge and a sponsor's size — and the two must agree about what the same
/// string hashes to. A framework hash would do the first and not the second across restarts,
/// and a club that changed its badge on the next API restart is a club the manager did not
/// rename. It is a digest and not a security hash: it is used to spread names over a list, not
/// to keep anything safe.
/// </para>
/// </summary>
public static class StableHash
{
    /// <summary>The digest of a name, as an unsigned 32-bit number.</summary>
    public static uint Of(string value)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;

        var hash = offset;

        foreach (var character in value)
        {
            hash ^= character;
            hash *= prime;
        }

        return hash;
    }
}