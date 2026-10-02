using NinjaEleven.Api.Contracts;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace NinjaEleven.Api.Controllers;

/// <summary>
/// Transport only for the youth academy: lists academy players, promotes them to the
/// first team, and manages the transfer list. All decisions live in <see cref="AcademyService"/>.
/// </summary>
[ApiController]
[Route("team")]
[Produces("application/json")]
public class AcademyController : ControllerBase
{
    private readonly AcademyService _academy;

    public AcademyController(AcademyService academy)
    {
        _academy = academy;
    }

    /// <summary>
    /// The full list of a club's academy players, ready for the Base screen.
    /// </summary>
    [HttpGet("{teamId:guid}/academy/{seasonId:guid}")]
    public async Task<ActionResult<IReadOnlyList<AcademyPlayerDto>>> ListAcademy(
        Guid teamId,
        Guid seasonId,
        CancellationToken cancellationToken)
    {
        var players = await _academy.ListAcademyPlayersAsync(teamId, seasonId, cancellationToken);
        return Ok(players.Select(p => new AcademyPlayerDto
        {
            PlayerId = p.PlayerId,
            Name = p.Name,
            Position = p.Position,
            Age = p.Age,
            OverallRating = p.OverallRating,
            Stars = p.Stars,
            Speed = p.Speed,
            Accuracy = p.Accuracy,
            Dribbling = p.Dribbling,
            Heading = p.Heading,
            Strength = p.Strength,
            GoalkeeperPower = p.GoalkeeperPower,
            Reflexes = p.Reflexes,
            Stamina = p.Stamina,
            Potential = p.Potential,
            DevelopmentRoom = p.DevelopmentRoom,
            Energy = p.Energy,
            IsAvailable = p.IsAvailable,
            Injury = p.Injury,
            InjuryMatchesRemaining = p.InjuryMatchesRemaining,
            SuspensionMatches = p.SuspensionMatches,
            Retiring = p.Retiring
        }));
    }

    /// <summary>
    /// Promotes an academy player to the first team, giving him a one-season contract
    /// at the minimum wage for his current profile.
    /// </summary>
    [HttpPost("{teamId:guid}/academy/{playerId:guid}/promote")]
    public async Task<ActionResult<PromoteAcademyResponseDto>> Promote(
        Guid teamId,
        Guid playerId,
        [FromQuery] Guid seasonId,
        CancellationToken cancellationToken)
    {
        var result = await _academy.PromoteAcademyPlayerAsync(teamId, playerId, seasonId, cancellationToken);

        return Ok(new PromoteAcademyResponseDto
        {
            PlayerId = result.PlayerId,
            PlayerName = result.PlayerName,
            TeamId = result.TeamId,
            TeamName = result.TeamName,
            ShirtNumber = result.ShirtNumber,
            Salary = result.Salary,
            SeasonsLeft = result.SeasonsLeft
        });
    }

    /// <summary>
    /// Places a first-team player on the transfer list, making him visible to buying clubs.
    /// </summary>
    [HttpPost("{teamId:guid}/transfer-list/{playerId:guid}")]
    public async Task<ActionResult<TransferListResultDto>> ListPlayer(
        Guid teamId,
        Guid playerId,
        [FromQuery] Guid seasonId,
        CancellationToken cancellationToken)
    {
        var result = await _academy.PutOnTransferListAsync(teamId, playerId, seasonId, cancellationToken);

        return Ok(new TransferListResultDto
        {
            PlayerId = result.PlayerId,
            PlayerName = result.PlayerName,
            TeamId = result.TeamId,
            TeamName = result.TeamName,
            OnTransferList = result.OnTransferList
        });
    }

    /// <summary>
    /// Takes a player off the transfer list.
    /// </summary>
    [HttpDelete("{teamId:guid}/transfer-list/{playerId:guid}")]
    public async Task<ActionResult<TransferListResultDto>> UnlistPlayer(
        Guid teamId,
        Guid playerId,
        [FromQuery] Guid seasonId,
        CancellationToken cancellationToken)
    {
        var result = await _academy.TakeOffTransferListAsync(teamId, playerId, seasonId, cancellationToken);

        return Ok(new TransferListResultDto
        {
            PlayerId = result.PlayerId,
            PlayerName = result.PlayerName,
            TeamId = result.TeamId,
            TeamName = result.TeamName,
            OnTransferList = result.OnTransferList
        });
    }
}
