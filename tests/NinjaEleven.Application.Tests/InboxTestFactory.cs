using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Inbox;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// The box a service under test writes to.
///
/// Almost every test in this project builds a graph of real services over mocked
/// repositories, and the box is now one of the services in that graph. It is built here so
/// that adding a dependency to it is one line in one file rather than a constructor argument
/// in every test that happens to build a <see cref="FinanceService"/>.
///
/// A test that wants to read what was written passes its own repository mock and keeps it;
/// a test that is about something else lets the box write into a mock nobody looks at. The
/// rule about who receives a message — only a club somebody is running — is asked of the team
/// repository, so a test whose teams are not marked as the manager's club simply has nothing
/// delivered, which is the truth about that world rather than a failure of the test.
/// </summary>
internal static class InboxTestFactory
{
    /// <summary>A box whose messages go nowhere, for a test that is not about the box.</summary>
    public static InboxService Create(Mock<ITeamRepository> teams) =>
        Create(teams, new Mock<IInboxMessageRepository>());

    /// <summary>A box over a repository the caller keeps, for a test that reads the messages.</summary>
    public static InboxService Create(
        Mock<ITeamRepository> teams,
        Mock<IInboxMessageRepository> messages)
    {
        messages.Setup(repo => repo.ExistsWithReferenceAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        messages.Setup(repo => repo.AddAsync(It.IsAny<InboxMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        messages.Setup(repo => repo.UnreadCountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        return new InboxService(
            messages.Object,
            teams.Object,
            // A world of nobody, which is what a test that has not declared a manager has. The
            // two messages with no addressee of their own — the cup round and the season's
            // summary — are delivered to the managers of the world, so a test that is about
            // something else pays for no fan-out and gets none.
            new ManagedClubs(),
            Mock.Of<IUnitOfWork>(),
            NullLogger<InboxService>.Instance);
    }

    /// <summary>
    /// A box over a world with managers in it, for a test that is about the two messages
    /// written to everybody.
    /// </summary>
    public static InboxService Create(
        Mock<ITeamRepository> teams,
        Mock<IInboxMessageRepository> messages,
        IManagedClubReader managedClubs)
    {
        messages.Setup(repo => repo.ExistsWithReferenceAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        messages.Setup(repo => repo.AddAsync(It.IsAny<InboxMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        messages.Setup(repo => repo.UnreadCountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        return new InboxService(
            messages.Object,
            teams.Object,
            managedClubs,
            Mock.Of<IUnitOfWork>(),
            NullLogger<InboxService>.Instance);
    }
}
