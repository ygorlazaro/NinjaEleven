using Microsoft.Extensions.Options;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// The three things a match service now needs to know about the world it is playing in.
///
/// <para>
/// They are here rather than repeated in each test class because they are the same in every
/// one of them: a test that is about a goal does not care what time it is or which process
/// the match belongs to, and a test that changes those things to suit itself has stopped
/// being about the goal. The clock is a fixed instant so a lease is a fact in an assertion
/// rather than a comparison with whatever the machine felt like.
/// </para>
/// </summary>
public static class MatchTestContext
{
    /// <summary>A fixed instant, so a lease and a claim are numbers rather than a race.</summary>
    public static readonly DateTimeOffset Now = new(2026, 3, 1, 16, 0, 0, TimeSpan.Zero);

    /// <summary>The identity of the process running the test.</summary>
    public static IMatchHost Host { get; } = new ProcessMatchHost("test-host");

    /// <summary>The world's own tolerances, at their defaults.</summary>
    public static IOptions<WorldExecutionOptions> World() => Options.Create(new WorldExecutionOptions());

    /// <summary>A clock that only ever says <see cref="Now"/> until a test moves it.</summary>
    public static IClock Clock { get; } = new FixedClock(Now);

    /// <summary>
    /// Moves the fixed clock forward.
    ///
    /// <para>
    /// The windows a match waits in are counted in seconds of waiting, so they can only be
    /// tested by a clock that goes somewhere: a test that cannot move time cannot say that a
    /// penalty left the manager's hands after fifteen seconds rather than never.
    /// </para>
    /// </summary>
    public static void Advance(TimeSpan by) => ((FixedClock)Clock).UtcNow = Clock.UtcNow.Add(by);

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
    }
}
