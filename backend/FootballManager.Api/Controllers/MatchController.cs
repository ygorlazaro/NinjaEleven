using FootballManager.Api.Contracts;
using FootballManager.Api.Mappings;
using FootballManager.Api.Realtime;
using FootballManager.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace FootballManager.Api.Controllers;

/// <summary>
/// Match endpoints. The read side is served from the persisted snapshot; the state
/// changing commands drive the match engine, and the hub stays a thin adapter over
/// the same service.
/// </summary>
[ApiController]
[Route("match")]
[Produces("application/json")]
public class MatchController : ControllerBase
{
    private readonly MatchService _matchService;
    private readonly MatchSimulator _simulator;

    public MatchController(MatchService matchService, MatchSimulator simulator)
    {
        _matchService = matchService;
        _simulator = simulator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MatchDto>>> List(CancellationToken cancellationToken)
    {
        var matches = await _matchService.GetAllAsync(cancellationToken);
        return Ok(matches.ToDtos());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MatchDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var snapshot = await _matchService.GetSnapshotAsync(id, cancellationToken);
        return Ok(snapshot.ToDto());
    }

    /// <summary>
    /// Events published after a given sequence, so a reconnecting client can fill the
    /// gap without downloading the whole feed again.
    /// </summary>
    [HttpGet("{id:guid}/event")]
    public async Task<ActionResult<IReadOnlyList<MatchEventDto>>> GetEvents(
        Guid id,
        [FromQuery] int? afterSequence,
        CancellationToken cancellationToken)
    {
        var events = await _matchService.GetEventsAsync(id, afterSequence, cancellationToken);
        return Ok(events.ToDtos());
    }

    /// <summary>
    /// Creates the playable session for a fixture, locks the starting eleven of both
    /// clubs and starts the other matches of the round at the same time. A fixture that
    /// is already being played answers with the running match instead of a conflict, so
    /// the caller can watch it.
    /// </summary>
    [HttpPost("start/{fixtureId:guid}")]
    public async Task<ActionResult<MatchCommandResultDto>> Start(
        Guid fixtureId,
        [FromBody] StartMatchRequestDto? request,
        CancellationToken cancellationToken)
    {
        var matchId = await _simulator.KickOffMatchdayAsync(
            fixtureId,
            request?.UserTeamId,
            request?.StarterIds,
            request?.Seed,
            cancellationToken);

        if (!matchId.HasValue)
        {
            return Conflict(new MatchCommandResultDto
            {
                Accepted = false,
                ErrorMessage = "Esta partida não pode ser iniciada agora."
            });
        }

        return Ok(new MatchCommandResultDto
        {
            Accepted = true,
            MatchId = matchId.Value
        });
    }

    /// <summary>
    /// Plays a fixture to full time without a live session, for the fixtures the
    /// manager is not watching. Same engine, same events, same final row.
    /// </summary>
    [HttpPost("simulate/{fixtureId:guid}")]
    public async Task<ActionResult<MatchCommandResultDto>> Simulate(
        Guid fixtureId,
        CancellationToken cancellationToken)
    {
        var matchId = await _simulator.SimulateFixtureAsync(fixtureId, cancellationToken);

        if (!matchId.HasValue)
        {
            return Conflict(new MatchCommandResultDto
            {
                Accepted = false,
                ErrorMessage = "This fixture is not waiting to be played."
            });
        }

        return Ok(new MatchCommandResultDto
        {
            Accepted = true,
            MatchId = matchId.Value
        });
    }

    /// <summary>
    /// The eleven and the bench of both clubs, as locked in at kick-off.
    /// </summary>
    [HttpGet("lineup/{matchId:guid}")]
    public async Task<ActionResult<MatchLineupDto>> GetLineup(
        Guid matchId,
        [FromQuery] Guid? userTeamId,
        CancellationToken cancellationToken)
    {
        var lineup = await _matchService.GetLineupAsync(matchId, userTeamId, cancellationToken);
        return Ok(lineup.ToDto());
    }

    /// <summary>
    /// Live state of a match. Served from the engine's working memory while it runs
    /// and from the persisted row once it is over.
    /// </summary>
    [HttpGet("state/{matchId:guid}")]
    public async Task<ActionResult<MatchStateDto>> GetState(
        Guid matchId,
        CancellationToken cancellationToken)
    {
        var state = await _matchService.GetStateAsync(matchId, cancellationToken);
        return Ok(state.ToDto());
    }

    /// <summary>
    /// Advances the match by one tick and answers the events it produced. The hub
    /// drives this automatically; the command stays available for stepping a match
    /// manually and for tests.
    /// </summary>
    [HttpPost("tick/{matchId:guid}")]
    public async Task<ActionResult<IReadOnlyList<MatchEngineEventDto>>> Tick(
        Guid matchId,
        CancellationToken cancellationToken)
    {
        var result = await _matchService.TickAsync(matchId, cancellationToken);
        return Ok(result.Events.ToDtos());
    }

    /// <summary>
    /// Leaves the half-time pause and starts the second half.
    /// </summary>
    [HttpPost("continue-second-half/{matchId:guid}")]
    public async Task<ActionResult<MatchCommandResultDto>> ContinueSecondHalf(
        Guid matchId,
        CancellationToken cancellationToken) =>
        Ok((await _matchService.ContinueSecondHalfAsync(matchId, cancellationToken)).ToDto());

    [HttpPost("substitution/{matchId:guid}/team/{teamId:guid}")]
    public async Task<ActionResult<MatchCommandResultDto>> Substitute(
        Guid matchId,
        Guid teamId,
        [FromBody] SubstitutionRequestDto request,
        CancellationToken cancellationToken) =>
        Ok((await _matchService.SubstituteAsync(
                matchId, teamId, request.PlayerOutId, request.PlayerInId, cancellationToken))
            .ToDto());

    [HttpPost("penalty-taker/{matchId:guid}/team/{teamId:guid}")]
    public async Task<ActionResult<MatchCommandResultDto>> SelectPenaltyTaker(
        Guid matchId,
        Guid teamId,
        [FromBody] PenaltyTakerRequestDto request,
        CancellationToken cancellationToken) =>
        Ok((await _matchService.SelectPenaltyTakerAsync(matchId, teamId, request.PlayerId, cancellationToken))
            .ToDto());

    [HttpPost("pause/{matchId:guid}")]
    public async Task<ActionResult<MatchCommandResultDto>> Pause(
        Guid matchId,
        CancellationToken cancellationToken) =>
        Ok((await _matchService.PauseAsync(matchId, cancellationToken)).ToDto());

    [HttpPost("resume/{matchId:guid}")]
    public async Task<ActionResult<MatchCommandResultDto>> Resume(
        Guid matchId,
        CancellationToken cancellationToken) =>
        Ok((await _matchService.ResumeAsync(matchId, cancellationToken)).ToDto());

    [HttpPost("speed/{matchId:guid}/{speed:int}")]
    public async Task<ActionResult<MatchCommandResultDto>> ChangeSpeed(
        Guid matchId,
        int speed,
        CancellationToken cancellationToken) =>
        Ok((await _matchService.ChangeSpeedAsync(matchId, speed, cancellationToken)).ToDto());

    /// <summary>
    /// Result of the match, served once it is over.
    /// </summary>
    [HttpGet("result/{matchId:guid}")]
    public async Task<ActionResult<MatchResultDto>> GetResult(
        Guid matchId,
        CancellationToken cancellationToken)
    {
        var result = await _matchService.GetResultAsync(matchId, cancellationToken);
        return Ok(result.ToDto());
    }
}
