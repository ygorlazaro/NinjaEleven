using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// A match's working memory lives in the process that kicked it off, and the world is played
/// by more than one process now.
///
/// <para>
/// So a row has to say whose memory it belongs to, and it has to stop belonging to a process
/// that is gone. Without the first half, a restart abandons somebody else's football. Without
/// the second, a process killed at minute sixty leaves a fixture in progress for ever and a
/// round that can never be completed. These are the two halves, and they are tested apart
/// because they are apart: the first is about who may take a match, the second is about how
/// long they have.
/// </para>
/// </summary>
public class MatchSessionLeaseTests
{
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 16, 0, 0, TimeSpan.Zero);

    private static Match ALiveMatch() =>
        Match.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void A_match_that_has_been_played_is_in_nobody_ways_memory()
    {
        var match = ALiveMatch();
        match.ClaimSession("api-1", Now);

        match.Finish();

        Assert.Null(match.SessionHeartbeatAt);
        Assert.False(match.IsLive);
    }

    [Fact]
    public void A_match_owned_by_another_process_is_not_ours_to_reclaim()
    {
        var match = ALiveMatch();
        match.ClaimSession("scheduler-1", Now);

        Assert.False(match.CanBeReclaimedBy("api-1", Now.AddSeconds(30), Lease));
    }

    [Fact]
    public void Our_own_match_is_ours_to_reclaim_the_moment_the_process_starts_again()
    {
        // The process restarted, so the memory this match was in does not exist any more.
        // Waiting out the lease would leave the fixture in progress for five minutes of
        // nothing, which is a round that does not move.
        var match = ALiveMatch();
        match.ClaimSession("api-1", Now);

        Assert.True(match.CanBeReclaimedBy("api-1", Now.AddSeconds(2), Lease));
    }

    [Fact]
    public void A_match_whose_owner_stopped_renewing_its_lease_becomes_anybody_s_to_take()
    {
        var match = ALiveMatch();
        match.ClaimSession("scheduler-1", Now);

        Assert.True(match.CanBeReclaimedBy("api-1", Now.AddMinutes(6), Lease));
    }

    [Fact]
    public void A_tick_renews_the_lease()
    {
        var match = ALiveMatch();
        match.ClaimSession("scheduler-1", Now);

        match.TouchSession(Now.AddMinutes(4));

        Assert.False(match.CanBeReclaimedBy("api-1", Now.AddMinutes(6), Lease));
        Assert.True(match.CanBeReclaimedBy("api-1", Now.AddMinutes(10), Lease));
    }

    [Fact]
    public void A_match_written_before_there_were_hosts_belongs_to_nobody_and_is_free()
    {
        // A row from a world that was played before this column existed. It has no owner and
        // no heartbeat, and refusing to touch it would strand every match the old world had
        // in progress.
        var match = ALiveMatch();

        Assert.True(match.CanBeReclaimedBy("api-1", Now, Lease));
    }

    [Theory]
    [InlineData(MatchStatus.KickOff, true)]
    [InlineData(MatchStatus.InProgress, true)]
    [InlineData(MatchStatus.HalfTime, true)]
    [InlineData(MatchStatus.SecondHalf, true)]
    [InlineData(MatchStatus.Scheduled, false)]
    [InlineData(MatchStatus.Finished, false)]
    [InlineData(MatchStatus.Abandoned, false)]
    public void Only_the_stretch_between_the_whistles_is_somebody_s_to_hold(MatchStatus status, bool live)
    {
        // A match that has not kicked off is nobody's, and a match that has finished belongs
        // to history. Only the ninety minutes in between are a claim worth making.
        var match = ALiveMatch();
        typeof(Match)
            .GetProperty(nameof(Match.Status))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(match, new object[] { status });

        Assert.Equal(live, match.IsLive);
    }
}
