using FootballManager.Api.Contracts;
using FootballManager.Api.Mappings;
using FootballManager.Api.Realtime;
using FootballManager.Application.Models;
using FootballManager.Application.Services;
using Microsoft.AspNetCore.SignalR;

namespace FootballManager.Api.Realtime;

/// <summary>
/// Real time channel of a match. The hub is deliberately thin: it receives a command
/// DTO, calls the service and sends the resulting DTOs back. It never decides a goal,
/// a possession or a substitution, and it never touches a repository or EF Core.
/// </summary>
public class MatchHub : Hub
{
    public const string EventMethod = "MatchEvent";
    public const string StateMethod = "MatchState";
    public const string ResultMethod = "MatchFinished";

    private readonly MatchService _matchService;
    private readonly IMatchBroadcaster _broadcaster;
    private readonly ILogger<MatchHub> _logger;

    public MatchHub(MatchService matchService, IMatchBroadcaster broadcaster, ILogger<MatchHub> logger)
    {
        _matchService = matchService;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    public static string GroupFor(Guid matchId) => $"match:{matchId}";

    /// <summary>
    /// Joins a client to the stream of one match. The current state is sent back right
    /// away, which is exactly what a reconnecting client needs before following the new
    /// events.
    /// </summary>
    public async Task<MatchStateDto> SubscribeMatch(Guid matchId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(matchId), Context.ConnectionAborted);

        _logger.LogInformation(
            "Connection {ConnectionId} subscribed to match {MatchId}",
            Context.ConnectionId,
            matchId);

        var state = (await _matchService.GetStateAsync(matchId, Context.ConnectionAborted)).ToDto();
        await Clients.Caller.SendAsync(StateMethod, state, Context.ConnectionAborted);

        return state;
    }

    public async Task LeaveMatch(Guid matchId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(matchId), Context.ConnectionAborted);

        _logger.LogInformation(
            "Connection {ConnectionId} left match {MatchId}",
            Context.ConnectionId,
            matchId);
    }

    public Task<MatchCommandResultDto> MakeSubstitution(MakeSubstitutionDto request) =>
        ExecuteAsync(
            request.MatchId,
            "substitution",
            matchId => _matchService.SubstituteAsync(
                matchId,
                request.TeamId,
                request.PlayerOutId,
                request.PlayerInId,
                Context.ConnectionAborted));

    public Task<MatchCommandResultDto> SelectPenaltyTaker(SelectPenaltyTakerDto request) =>
        ExecuteAsync(
            request.MatchId,
            "penalty-taker",
            matchId => _matchService.SelectPenaltyTakerAsync(
                matchId,
                request.TeamId,
                request.PlayerId,
                Context.ConnectionAborted));

    public Task<MatchCommandResultDto> PauseMatch(MatchCommandDto request) =>
        ExecuteAsync(
            request.MatchId,
            "pause",
            matchId => _matchService.PauseAsync(matchId, Context.ConnectionAborted));

    public Task<MatchCommandResultDto> ResumeMatch(MatchCommandDto request) =>
        ExecuteAsync(
            request.MatchId,
            "resume",
            matchId => _matchService.ResumeAsync(matchId, Context.ConnectionAborted));

    public Task<MatchCommandResultDto> ChangeMatchSpeed(MatchSpeedDto request) =>
        ExecuteAsync(
            request.MatchId,
            "speed",
            matchId => _matchService.ChangeSpeedAsync(matchId, request.Speed, Context.ConnectionAborted));

    /// <summary>
    /// Leaves the half-time pause. The client decides when, because it is the one that
    /// shows the interval to the user.
    /// </summary>
    public Task<MatchCommandResultDto> ContinueSecondHalf(MatchCommandDto request) =>
        ExecuteAsync(
            request.MatchId,
            "continue-second-half",
            matchId => _matchService.ContinueSecondHalfAsync(matchId, Context.ConnectionAborted));

    /// <summary>
    /// Runs one command and republishes whatever it produced, so every follower of the
    /// match sees the same ordered stream no matter which client issued the command.
    /// </summary>
    private async Task<MatchCommandResultDto> ExecuteAsync(
        Guid matchId,
        string commandName,
        Func<Guid, Task<MatchCommandResult>> command)
    {
        var result = await command(matchId);
        var dto = result.ToDto();

        if (!result.Accepted)
        {
            _logger.LogWarning(
                "Command {Command} refused on match {MatchId}: {Reason}",
                commandName,
                matchId,
                result.ErrorMessage);
        }

        await _broadcaster.PublishEventsAsync(matchId, dto.Events, Context.ConnectionAborted);
        await PublishStateAsync(matchId);

        return dto;
    }

    private async Task PublishStateAsync(Guid matchId)
    {
        var state = (await _matchService.GetStateAsync(matchId, Context.ConnectionAborted)).ToDto();
        await _broadcaster.PublishStateAsync(matchId, state, Context.ConnectionAborted);
    }
}
