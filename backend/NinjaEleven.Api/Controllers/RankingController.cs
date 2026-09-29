using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NinjaEleven.Api.Contracts;
using NinjaEleven.Api.Mappings;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Seasons;

namespace NinjaEleven.Api.Controllers;

/// <summary>
/// Club ranking endpoints.
/// </summary>
[ApiController]
[Route("ranking")]
[Produces("application/json")]
[Authorize]
public class RankingController : ControllerBase
{
    private readonly ClubRankingService _rankingService;
    private readonly ISeasonRepository _seasons;

    public RankingController(ClubRankingService rankingService, ISeasonRepository seasons)
    {
        _rankingService = rankingService;
        _seasons = seasons;
    }

    /// <summary>
    /// Gets the Ninja Ranking for all clubs.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClubRankingDto>>> GetRanking(
        CancellationToken cancellationToken)
    {
        var currentSeason = await _seasons.GetCurrentAsync(cancellationToken);
        if (currentSeason is null)
        {
            return Ok(Array.Empty<ClubRankingDto>());
        }

        var entries = await _rankingService.CalculateRankingAsync(currentSeason.Id, cancellationToken);
        return Ok(entries.Select(e => e.ToDto()).ToList());
    }
}