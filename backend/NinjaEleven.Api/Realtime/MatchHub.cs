using NinjaEleven.Api.Contracts;
using NinjaEleven.Api.Mappings;
using NinjaEleven.Api.Realtime;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
using Microsoft.AspNetCore.SignalR;

namespace NinjaEleven.Api.Realtime;

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
    public const string ScoreMethod = "MatchScore";

    /// <summary>
    /// The beats of a match of the round, for a client that is not watching that match in
    /// full. It carries the match id, because a client following a whole matchday receives
    /// four of these and has to know which log each one belongs to.
    /// </summary>
    public const string MatchdayEventMethod = "MatchdayEvent";

    private readonly MatchService _matchService;
    private readonly MatchCommandPublisher _publisher;
    private readonly ILogger<MatchHub> _logger;

    public MatchHub(MatchService matchService, MatchCommandPublisher publisher, ILogger<MatchHub> logger)
    {
        _matchService = matchService;
        _publisher = publisher;
        _logger = logger;
    }

    public static string MatchGroupFor(Guid matchId) => $"match:{matchId}";

    /// <summary>Group of a whole matchday, used for the score of the other matches.</summary>
    public static string RoundGroupFor(Guid roundId) => $"round:{roundId}";

    /// <summary>
    /// Joins a client to the stream of one match. The current state is sent back right
    /// away, which is exactly what a reconnecting client needs before following the new
    /// events.
    /// </summary>
    public async Task<MatchStateDto> SubscribeMatch(Guid matchId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, MatchGroupFor(matchId), Context.ConnectionAborted);

        _logger.LogInformation(
            "Connection {ConnectionId} subscribed to match {MatchId}",
            Context.ConnectionId,
            matchId);

        var state = (await _matchService.GetStateAsync(matchId, Context.ConnectionAborted)).ToDto();
        await Clients.Caller.SendAsync(StateMethod, state, Context.ConnectionAborted);

        return state;
    }

    /// <summary>
    /// Joins the score stream of a round. The client watches one match in full and the
    /// rest of the matchday on a scoreboard, both over the same connection.
    /// </summary>
    public async Task SubscribeMatchday(Guid roundId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, RoundGroupFor(roundId), Context.ConnectionAborted);

        _logger.LogInformation(
            "Connection {ConnectionId} subscribed to round {RoundId}",
            Context.ConnectionId,
            roundId);
    }

    public async Task LeaveMatchday(Guid roundId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, RoundGroupFor(roundId), Context.ConnectionAborted);
    }

    public async Task LeaveMatch(Guid matchId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, MatchGroupFor(matchId), Context.ConnectionAborted);

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

    public Task<MatchCommandResultDto> NameShootoutOrder(NameShootoutOrderDto request) =>
        ExecuteAsync(
            request.MatchId,
            "shootout-order",
            matchId => _matchService.NameShootoutOrderAsync(
                matchId,
                request.TeamId,
                request.TakerIds,
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
    /// Claims a headless match of the manager's own club, handing the keyboard over from the
    /// engine to the manager.
    ///
    /// A cup window starts every fixture at once, so a manager who opens his club's match
    /// while it is already being played finds a session with no manager: the engine is
    /// playing both sides, the interval passes on its own and a substitution is refused.
    /// This hands control back without restarting a match the matchday has already seen
    /// kick off — the score and the eleven are kept, and only the control plane flips. The
    /// server answers for the manager's own club: a claim for any other side is refused,
    /// so a screen that followed a link to another club's match simply leaves the engine
    /// playing it.
    ///
    /// The claimed state is pushed straight away, so the substitution buttons and the
    /// half-time prompt appear without waiting for the next tick.
    /// </summary>
    public async Task<Guid> AttachManager(Guid matchId, Guid userTeamId)
    {
        var attached = _matchService.AttachManager(matchId, userTeamId);

        if (attached)
        {
            await _publisher.PublishAsync(matchId, new MatchCommandResultDto
            {
                Accepted = true,
                MatchId = matchId,
                Events = Array.Empty<MatchEngineEventDto>()
            }, Context.ConnectionAborted);
        }

        return matchId;
    }

    /// <summary>
    /// Runs one command and republishes whatever it produced, so every follower of the
    /// match sees the same ordered stream no matter which client issued the command.
    /// </summary>
    private async Task<MatchCommandResultDto> ExecuteAsync(
        Guid matchId,
        string commandName,
        Func<Guid, Task<MatchCommandResult>> command)
    {
        MatchCommandResult result;

        try
        {
            result = await command(matchId);
        }
        catch (DomainValidationException exception)
        {
            // A refused command is an answer, not a broken hub call: an illegal eleven, a
            // player who is not on the pitch or a penalty of the other club come back as
            // a result the client can render, with the rule that refused it.
            _logger.LogWarning(
                "Command {Command} refused on match {MatchId}: {Code} {Reason}",
                commandName,
                matchId,
                exception.Code,
                exception.Message);

            result = new MatchCommandResult
            {
                Accepted = false,
                MatchId = matchId,
                ErrorMessage = exception.Message
            };
        }

        if (!result.Accepted)
        {
            _logger.LogWarning(
                "Command {Command} refused on match {MatchId}: {Reason}",
                commandName,
                matchId,
                result.ErrorMessage);
        }

        return await _publisher.PublishAsync(matchId, result.ToDto(), Context.ConnectionAborted);
    }
}
