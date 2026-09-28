using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NinjaEleven.Api.Contracts;
using NinjaEleven.Api.Mappings;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Managers;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Api.Controllers;

/// <summary>
/// Account endpoints: register, login, change password, and the club selector for
/// logged-out users. Returns a JWT bearer token that the frontend stores and sends back
/// in subsequent requests.
/// </summary>
[ApiController]
[Route("auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly JwtOptions _jwtOptions;

    public AuthController(AuthService authService, IOptions<JwtOptions> jwtOptions)
    {
        _authService = authService;
        _jwtOptions = jwtOptions.Value;
    }

    /// <summary>
    /// Registers a new account. If a team id and coach name are provided, the manager row
    /// is created as part of registration and the career starts immediately.
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(
        [FromBody] AuthRegisterRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(
            request.Email,
            request.Password,
            request.CoachName,
            request.TeamId,
            cancellationToken);

        var token = GenerateToken(result.UserId, result.Email, result.TeamId, result.CoachName);

        return Ok(new AuthResponseDto
        {
            Token = token,
            UserId = result.UserId,
            Email = result.Email,
            TeamId = result.TeamId,
            CoachName = result.CoachName
        });
    }

    /// <summary>
    /// Signs in with an existing account, returning a JWT bearer token.
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(
        [FromBody] AuthLoginRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request.Email, request.Password, cancellationToken);

        var token = GenerateToken(result.UserId, result.Email, result.TeamId, result.CoachName);

        return Ok(new AuthResponseDto
        {
            Token = token,
            UserId = result.UserId,
            Email = result.Email,
            TeamId = result.TeamId,
            CoachName = result.CoachName
        });
    }

    /// <summary>
    /// Changes the password of the authenticated user.
    /// </summary>
    [HttpPut("password")]
    [Authorize]
    public async Task<ActionResult> ChangePassword(
        [FromBody] ChangePasswordRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        await _authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, cancellationToken);
        return Ok();
    }

    /// <summary>
    /// The clubs without a human manager, for the club-selection flow.
    /// </summary>
    [HttpGet("available-clubs")]
    public async Task<ActionResult<IReadOnlyList<TeamDto>>> GetAvailableClubs(
        CancellationToken cancellationToken)
    {
        var clubs = await _authService.GetAvailableClubsAsync(cancellationToken);
        return Ok(clubs.Select(team => team.ToDto()).ToList());
    }

    /// <summary>
    /// Links the authenticated user to a newly created manager, claiming a club.
    /// A user who registered without a team returns here to pick one.
    /// </summary>
    [HttpPost("link-manager")]
    [Authorize]
    public async Task<ActionResult<ManagerDto>> LinkManager(
        [FromBody] LinkManagerRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        await _authService.LinkManagerAsync(userId, request.TeamId, request.CoachName, cancellationToken);

        var manager = await _authService.GetManagerByUserIdAsync(userId, cancellationToken);
        if (manager is null) return StatusCode(500, "Manager was not created.");
        return Ok(manager.ToDto());
    }

    private string GenerateToken(Guid userId, string email, Guid? teamId, string? coachName)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, email),
        };

        if (teamId.HasValue)
        {
            claims.Add(new Claim("team_id", teamId.Value.ToString()));
        }

        if (!string.IsNullOrEmpty(coachName))
        {
            claims.Add(new Claim("coach_name", coachName));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.ExpiresMinutes).DateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? throw new DomainValidationException("Unauthorized", "No user id in the token.");

        return Guid.Parse(userIdClaim);
    }
}
