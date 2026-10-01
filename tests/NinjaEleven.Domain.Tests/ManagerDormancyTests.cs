using NinjaEleven.Domain.Managers;
using NinjaEleven.Domain.Users;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// The rules an account keeps about being here.
///
/// Every one of these is a question the world asks without a person in the room: is this
/// manager still turning up, and if not, who runs his club today. The thirty days is the
/// whole of it, and it is worth pinning down here rather than only in the sweep that reads
/// it, because a threshold that can move is a threshold nobody can reason about.
/// </summary>
public class ManagerDormancyTests
{
    private static readonly DateTimeOffset Today = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static User AnAccount() => User.Create("manager@example.com", "$2b$hash");

    [Fact]
    public void AnAccountThatHasJustRegisteredHasBeenHere()
    {
        var account = AnAccount();

        // A manager is dismissed for going away, and an account created a moment ago has not
        // gone anywhere. Leaving LastLoginAt null on creation would say the opposite, and the
        // sweep — which reads null as "never came back" — would fire on the account that has
        // been in the room the whole time.
        Assert.NotNull(account.LastLoginAt);
        Assert.True(account.IsActive);
        Assert.False(account.HasBeenAwayFor(ManagerDormancy.MaxDaysAway, Today));
    }

    [Fact]
    public void AManagerKeepsHisClubForThirtyDaysAndLosesItOnTheThirtyFirst()
    {
        var account = AnAccount();
        account.RecordLogin(Today);

        // Twenty-nine days and twenty-three hours is still a holiday.
        Assert.False(account.HasBeenAwayFor(ManagerDormancy.MaxDaysAway, Today.AddDays(29).AddHours(-1)));

        // The thirtieth day exactly is the boundary the rule names, and it belongs to the
        // club: "thirty days" is the last day the manager is owed.
        Assert.True(account.HasBeenAwayFor(ManagerDormancy.MaxDaysAway, Today.AddDays(30)));
    }

    [Fact]
    public void SigningInIsWhatKeepsAClub()
    {
        var account = AnAccount();
        account.RecordLogin(Today.AddDays(-29));
        account.RecordLogin(Today);

        // The clock runs from the last sign-in rather than from the first: a manager who comes
        // back on the twenty-ninth day is a manager who was there on the twenty-ninth day, and
        // reading the wrong one of the two dates would dismiss him a day after he arrived.
        Assert.False(account.HasBeenAwayFor(ManagerDormancy.MaxDaysAway, Today));
    }

    [Fact]
    public void ADismissedAccountIsMarkedInactiveRatherThanDeleted()
    {
        var account = AnAccount();
        account.RecordLogin(Today);
        account.Dismiss(Today.AddDays(31), Guid.NewGuid());

        Assert.False(account.IsActive);
        Assert.Equal(Today.AddDays(31), account.DismissedAt);

        // The row survives, and the email is still on it, because a person who comes back is
        // owed a club and not a second registration with a second password.
        Assert.Equal("manager@example.com", account.Email);
        Assert.NotEmpty(account.PasswordHash);
    }

    [Fact]
    public void AComingBackAccountIsAliveAndSignedInAgain()
    {
        var account = AnAccount();
        account.Dismiss(Today.AddDays(31), Guid.NewGuid());
        account.Reactivate(Today.AddDays(40));

        Assert.True(account.IsActive);
        Assert.Null(account.DismissedAt);
        Assert.Equal(Today.AddDays(40), account.LastLoginAt);
    }

    [Fact]
    public void AnAccountDismissedTwiceRemembersOnlyTheLatestOne()
    {
        // The dismissal is a date, not a history, and a manager who is let go, comes back and
        // is let go again has one dismissal that counts: the one that is keeping the club.
        var account = AnAccount();
        account.Dismiss(Today.AddDays(10), Guid.NewGuid());
        account.Reactivate(Today.AddDays(12));
        account.Dismiss(Today.AddDays(50), Guid.NewGuid());

        Assert.False(account.IsActive);
        Assert.Equal(Today.AddDays(50), account.DismissedAt);
    }
}
