using Microsoft.AspNetCore.Mvc;
using NinjaEleven.Api.Contracts;
using NinjaEleven.Application.Services;

namespace NinjaEleven.Api.Controllers;

/// <summary>
/// The manager who owns the career: the name chosen when the game began, and the club it
/// belongs to. Routes are singular, as the architecture requires.
/// </summary>
[ApiController]
[Route("team")]
[Produces("application/json")]
public class ManagerController : ControllerBase
{
    private readonly ManagerService _managerService;

    public ManagerController(ManagerService managerService)
    {
        _managerService = managerService;
    }

    /// <summary>
    /// The manager of a club, if the career has begun. Returns 404 when the club has no
    /// manager yet — the game has not started for this club.
    /// </summary>
    [HttpGet("{teamId:guid}/manager")]
    public async Task<ActionResult<ManagerDto>> Get(
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var manager = await _managerService.GetByTeamAsync(teamId, cancellationToken);
            return Ok(ToDto(manager));
        }
        catch (Domain.Common.EntityNotFoundException)
        {
            return NotFound(new { code = "ManagerNotFound", teamId });
        }
    }

    /// <summary>
    /// Creates the manager and starts the career. The club is addressed in the route so a
    /// manager is always tied to a club, and the name arrives in the body because it is the
    /// one piece of data that is not a key. A second call for the same club is refused: a
    /// career begins once.
    /// </summary>
    [HttpPost("{teamId:guid}/manager")]
    public async Task<ActionResult<ManagerDto>> Create(
        Guid teamId,
        [FromBody] ManagerCreateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var manager = await _managerService.CreateAsync(teamId, request.Name, cancellationToken);
            return Ok(ToDto(manager));
        }
        catch (Domain.Common.DomainValidationException ex)
        {
            return BadRequest(new { code = ex.Code, message = ex.Message });
        }
    }

    /// <summary>
    /// Changes the manager's name. The club is the manager's address, because a name change
    /// is a change to the club's manager, not to a manager found by id.
    /// </summary>
    [HttpPut("{teamId:guid}/manager")]
    public async Task<ActionResult<ManagerDto>> Rename(
        Guid teamId,
        [FromBody] ManagerRenameRequestDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var manager = await _managerService.RenameAsync(teamId, request.Name, cancellationToken);
            return Ok(ToDto(manager));
        }
        catch (Domain.Common.EntityNotFoundException)
        {
            return NotFound(new { code = "ManagerNotFound", teamId });
        }
    }

    private static ManagerDto ToDto(Domain.Managers.Manager manager) =>
        new()
        {
            Id = manager.Id,
            Name = manager.Name,
            TeamId = manager.TeamId,
            StartedAt = manager.StartedAt,
        };
}
