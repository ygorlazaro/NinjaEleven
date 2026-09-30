namespace NinjaEleven.Application.Abstractions;

/// <summary>
/// The world's clock, asked for rather than read.
///
/// "Now" is the one input a scheduler cannot invent, and a service that reads it from
/// <see cref="DateTimeOffset"/> directly is a service whose behaviour can only be checked by
/// waiting for it. Everything that answers "is this due yet" goes through this, which is why
/// a test can put a matchday in the past and watch the world move.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <inheritdoc />
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
