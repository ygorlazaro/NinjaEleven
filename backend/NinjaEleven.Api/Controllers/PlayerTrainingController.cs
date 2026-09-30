using NinjaEleven.Api.Contracts;
using NinjaEleven.Api.Mappings;
using NinjaEleven.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace NinjaEleven.Api.Controllers;

[ApiController]
[Route("player")]
[Produces("application/json")]
public class PlayerTrainingController : ControllerBase
{
    private readonly TrainingService _trainingService;

    public PlayerTrainingController(TrainingService trainingService)
    {
        _trainingService = trainingService;
    }

    /// <summary>
    /// Runs one training session and answers with what it cost and what it did.
    ///
    /// <para>
    /// It is a POST because it spends something, and the energy is gone whether or not the
    /// request arrives twice. A session behind a GET could be retried by a browser, a
    /// prefetcher or a manager double-tapping the button, and every one of those retries
    /// would be a free point — which is the one thing a feature that improves a man must not
    /// be.
    /// </para>
    /// </summary>
    [HttpPost("{id:guid}/training")]
    public async Task<ActionResult<TrainingResultDto>> Train(
        Guid id,
        [FromBody] TrainPlayerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _trainingService.TrainAsync(
            id, request.Attribute, request.SeasonId, cancellationToken);

        return Ok(new TrainingResultDto
        {
            PlayerId = result.PlayerId,
            SeasonId = result.SeasonId,
            Attribute = result.Attribute,
            EnergySpent = result.EnergySpent,
            EnergyLeft = result.EnergyLeft,
            AttributeBefore = result.AttributeBefore,
            AttributeAfter = result.AttributeAfter,
            Fee = result.Fee,
            SessionsLeft = result.SessionsLeft
        });
    }
}

/// <summary>
/// A club's training sheet, asked of the club rather than of each of its men.
///
/// <para>
/// It lives on the team route and not on the player route for the reason the read is one
/// call: a manager opening the training screen is asking about twenty-three men, and a
/// screen that fetched a price per player would be twenty-three requests to answer it, with
/// the last answer arriving after the first one had been acted on.
/// </para>
/// </summary>
[ApiController]
[Route("team")]
[Produces("application/json")]
public class TeamTrainingController : ControllerBase
{
    private readonly TrainingService _trainingService;

    public TeamTrainingController(TrainingService trainingService)
    {
        _trainingService = trainingService;
    }

    [HttpGet("{teamId:guid}/training")]
    public async Task<ActionResult<SquadTrainingQuotesDto>> GetQuotes(
        Guid teamId,
        [FromQuery] Guid? seasonId,
        CancellationToken cancellationToken)
    {
        var quotes = await _trainingService.QuoteAsync(teamId, seasonId, cancellationToken);

        return Ok(quotes.ToDto());
    }
}
