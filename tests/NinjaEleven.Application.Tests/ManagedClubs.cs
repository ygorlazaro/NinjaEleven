using NinjaEleven.Application.Abstractions;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// The world a service is told about when a test has no manager behind it.
/// </summary>
/// <remarks>
/// <para>
/// It is here for the same reason <see cref="InboxTestFactory"/> is: adding a dependency to a
/// service is otherwise a constructor argument in every test that builds it, and a test that
/// is about a transfer's money should not have to say anything about who is watching the
/// inbox.
/// </para>
///
/// <para>
/// A world of nobody is the truth about a test that has said nothing about managers, and it
/// is the answer that keeps the rule honest in the tests: a message is delivered to a club
/// because a person is running it, so a test that has not declared one is a world where
/// nobody is told anything, and a service that delivers anyway fails rather than passes.
/// </para>
/// </remarks>
internal sealed class ManagedClubs : IManagedClubReader
{
    private readonly Guid[] _clubIds;

    /// <summary>A world of nobody, which is what a test that has not declared a manager has.</summary>
    public ManagedClubs() : this([])
    {
    }

    /// <summary>A world where exactly these clubs have a person behind them.</summary>
    public ManagedClubs(params Guid[] clubIds)
    {
        _clubIds = clubIds;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Guid>> ListManagedClubsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Guid>>(_clubIds);
}
