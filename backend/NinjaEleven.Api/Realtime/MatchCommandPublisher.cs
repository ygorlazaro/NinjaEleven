using NinjaEleven.Api.Contracts;
using NinjaEleven.Api.Mappings;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Services;

namespace NinjaEleven.Api.Realtime;

/// <summary>
/// Republishes what a match command produced to everybody following the match.
///
/// A manager's substitution is a match event like any other, so whoever issued it, every
/// other screen has to see it: the engine's tick is published by the loop, the hub's own
/// commands are published by the hub, and a command that arrived over REST used to be
/// published by nobody at all. The result was that a manager's own changes were the only
/// events of a match that nothing narrated, and that a second screen watching the same
/// match saw them only after a reload.
///
/// Both transports go through here, so the rule is one rule: a command's events reach
/// every follower exactly once.
/// </summary>
public sealed class MatchCommandPublisher
{
    private readonly IMatchBroadcaster _broadcaster;
    private readonly MatchService _matchService;

    public MatchCommandPublisher(IMatchBroadcaster broadcaster, MatchService matchService)
    {
        _broadcaster = broadcaster;
        _matchService = matchService;
    }

    public async Task<MatchCommandResultDto> PublishAsync(
        Guid matchId,
        MatchCommandResult result,
        CancellationToken cancellationToken = default)
    {
        await _broadcaster.PublishEventsAsync(matchId, result.Events, cancellationToken);
        await PublishStateAsync(matchId, cancellationToken);

        return result.ToDto();
    }

    private async Task PublishStateAsync(Guid matchId, CancellationToken cancellationToken)
    {
        var state = await _matchService.GetStateAsync(matchId, cancellationToken);
        await _broadcaster.PublishStateAsync(matchId, state, cancellationToken);
    }
}
