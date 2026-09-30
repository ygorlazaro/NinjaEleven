using Microsoft.AspNetCore.Mvc;
using NinjaEleven.Api.Contracts;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Api.Controllers;

/// <summary>
/// The world, and the two questions a person needs to ask it while it is being debugged.
///
/// <para>
/// These are the same two calls the Scheduler makes, with the same answers, exposed over HTTP
/// so a world can be moved by hand. That is the whole of their purpose: "what does the
/// calendar say is due right now" and "play it". A developer testing the scheduler needs to
/// be able to ask the first without waiting for a cron and the second without starting a
/// process, and a route that reimplemented either of them would be a second opinion about the
/// world — which is the thing this architecture is arranged to prevent.
/// </para>
///
/// <para>
/// It is a development door, not an administrative one. There is no way to make the world
/// play something the calendar does not hold: <c>due</c> and <c>play-due</c> are answered by
/// the same service that answers them at five o'clock, a round that is not due stays not due
/// until its hour arrives, and <c>advance</c> walks the same calendar one window at a time
/// rather than inventing a fixture that is not on it.
/// </para>
/// </summary>
[ApiController]
[Route("world")]
[Produces("application/json")]
public class WorldController : ControllerBase
{
    private readonly CompetitionExecutionService _execution;

    public WorldController(CompetitionExecutionService execution) => _execution = execution;

    /// <summary>
    /// The windows of one kind of competition that the calendar says are due and have not
    /// been played. It is the question the championship and cup jobs ask, asked by hand.
    /// </summary>
    /// <param name="wave">Which window to look for. Defaults to the championship.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    [HttpGet("due")]
    public async Task<ActionResult<IReadOnlyList<DueRoundDto>>> GetDue(
        [FromQuery] CompetitionType? wave,
        CancellationToken cancellationToken)
    {
        var due = await _execution.GetDueRoundsAsync(wave ?? CompetitionType.League, cancellationToken);
        return Ok(due.Select(round => round.ToDto()).ToList());
    }

    /// <summary>
    /// Plays everything the calendar says is due of one kind of competition, right now.
    ///
    /// This is how a round is tested without waiting for its hour: the developer moves the
    /// clock, not the code. The same claim, the same idempotency and the same partial-failure
    /// rules apply as when the Scheduler fires it, because it is the same call.
    /// </summary>
    /// <param name="wave">Which window to play. Defaults to the championship.</param>
    /// <param name="teamId">The manager's own club, whose fixture is started and left for him.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    [HttpPost("play-due")]
    public async Task<ActionResult<IReadOnlyList<RoundRunDto>>> PlayDue(
        [FromQuery] CompetitionType? wave,
        [FromQuery] Guid? teamId,
        CancellationToken cancellationToken)
    {
        var runs = await _execution.PlayDueRoundsAsync(wave ?? CompetitionType.League, teamId, cancellationToken);
        return Ok(runs.Select(run => run.ToDto()).ToList());
    }

    /// <summary>
    /// Moves the world one step: the next window of the calendar, now, whatever the date says.
    ///
    /// <para>
    /// This is the door that replaces a world being hurried by a setting. A matchday is a day
    /// and the championship's window goes out at fifteen hundred, so waiting for the calendar
    /// means a developer who wants to see the sixth round of a season waits thirty-four days for
    /// it; each call here plays one window instead, in the order the calendar is drawn: the
    /// first round of every division, then the second, and on the seventh day the round of the
    /// divisions and the 32-avos of the cup in two calls, because they are six hours apart.
    /// </para>
    ///
    /// <para>
    /// The call that plays the final's second leg is also the call that closes the season, pays
    /// the championship purses, moves the pyramid between the divisions and opens the next
    /// season — whose first day has the Supercup of the two clubs that won the two competitions.
    /// </para>
    /// </summary>
    /// <param name="teamId">
    /// The manager's own club. A window that holds one of his fixtures opens that fixture and
    /// leaves it on the touchline for him — the same treatment the cup window gives it — while
    /// the rest of the day is played out. Without it the world plays his evening for him and
    /// hands him a result.
    /// </param>
    /// <param name="cancellationToken">Cancellation.</param>
    [HttpPost("advance")]
    public async Task<ActionResult<WorldAdvanceDto>> Advance(
        [FromQuery] Guid? teamId,
        CancellationToken cancellationToken)
    {
        var advance = await _execution.AdvanceTheWorldAsync(teamId, cancellationToken);
        return Ok(advance.ToDto());
    }
}
