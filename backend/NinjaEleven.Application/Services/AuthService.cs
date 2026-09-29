using Microsoft.Extensions.Logging;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Managers;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Domain.Users;

namespace NinjaEleven.Application.Services;

/// <summary>
/// Handles account creation, login, and password changes. This is the only service that
/// writes user rows, so the rules around them stay in one place.
/// 
/// On registration, a club is automatically assigned:
/// - Prefers clubs without human manager that are still alive in the current cup
/// - Among those, picks the weakest club (by squad strength)
/// - If no clubs alive in cup, picks the strongest among the weakest available clubs
/// </summary>
public class AuthService
{
    private readonly IUserRepository _users;
    private readonly IManagerRepository _managers;
    private readonly ITeamRepository _teams;
    private readonly ICompetitionRepository _competitions;
    private readonly ISeasonRepository _seasons;
    private readonly ICupTieRepository _cupTies;
    private readonly StandingsService _standings;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        IManagerRepository managers,
        ITeamRepository teams,
        ICompetitionRepository competitions,
        ISeasonRepository seasons,
        ICupTieRepository cupTies,
        StandingsService standings,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        ILogger<AuthService> logger)
    {
        _users = users;
        _managers = managers;
        _teams = teams;
        _competitions = competitions;
        _seasons = seasons;
        _cupTies = cupTies;
        _standings = standings;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Registers a new user and automatically assigns them a club.
    /// The club is chosen based on:
    /// 1. Clubs without human manager in current season
    /// 2. Preference for clubs still alive in the cup
    /// 3. Among those, the weakest club (by squad strength)
    /// 4. Fallback: strongest among the weakest clubs if none in cup
    /// </summary>
    public async Task<AuthResult> RegisterAsync(
        string email,
        string password,
        string? coachName,
        Guid? teamId, // Ignored - club is auto-assigned
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainValidationException("InvalidEmail", "An email address is required.");

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            throw new DomainValidationException(
                "WeakPassword", "The password must be at least six characters long.");

        var existing = await _users.GetByEmailAsync(email, cancellationToken);
        if (existing is not null)
            throw new DomainValidationException(
                "EmailAlreadyInUse", $"The email '{email}' is already registered.");

        var passwordHash = _passwordHasher.Hash(password);
        var user = User.Create(email, passwordHash);

        // Auto-assign a club
        var assignedTeam = await AutoAssignClubAsync(cancellationToken);
        
        // Check if the assigned team already has a manager (shouldn't happen but handle it)
        var existingManager = await _managers.GetByTeamAsync(assignedTeam.Id, cancellationToken);
        if (existingManager is not null)
        {
            _logger.LogWarning("Assigned club {ClubName} already has a manager, removing existing manager", assignedTeam.Name);
            _managers.Remove(existingManager);
        }
        
        var manager = Manager.Create(assignedTeam.Id, coachName ?? "Técnico");
        manager.SetUserId(user.Id);
        user.SetManager(manager);

        await _teams.MarkAsManagerClubAsync(assignedTeam.Id, cancellationToken);
        await _managers.AddAsync(manager, cancellationToken);

        await _users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User registered: {Email} assigned to {ClubName} as {CoachName}.",
            email,
            assignedTeam.Name,
            manager.Name);

        return new AuthResult(user.Id, user.Email, user.Manager?.TeamId, manager.Name);
    }

    /// <summary>
    /// Automatically assigns a club to a new user.
    /// </summary>
    private async Task<Team> AutoAssignClubAsync(CancellationToken cancellationToken)
    {
        // Get current season
        var currentSeason = await _seasons.GetCurrentAsync(cancellationToken)
            ?? throw new DomainValidationException("NoCurrentSeason", "There is no current season.");

        // Get all clubs without human manager in current season (participating in league)
        var clubsWithoutManager = await _teams.ListClubsWithoutManagerInSeasonAsync(currentSeason.Id, cancellationToken);
        
        // Fallback: if no clubs are enrolled in league competitions yet, use all clubs without managers
        if (clubsWithoutManager.Count == 0)
        {
            clubsWithoutManager = await _teams.ListClubsWithoutManagerAsync(cancellationToken);
        }
        
        if (clubsWithoutManager.Count == 0)
        {
            throw new DomainValidationException("NoAvailableClubs", "All clubs already have human managers.");
        }

        // Get cup edition for current season
        var cupEdition = await GetCupEditionForSeasonAsync(currentSeason.Id, cancellationToken);
        
        // Find clubs still alive in the cup
        var clubsInCup = new List<Team>();
        if (cupEdition.HasValue)
        {
            var aliveClubIds = await _cupTies.GetAliveClubsInCupAsync(cupEdition.Value, cancellationToken);
            clubsInCup = clubsWithoutManager.Where(c => aliveClubIds.Contains(c.Id)).ToList();
        }

        // Pre-calculate all needed data in bulk to avoid N+1 queries
        var clubData = await CalculateAllClubDataAsync(clubsWithoutManager, currentSeason.Id, cancellationToken);
        var clubDataById = clubData.ToDictionary(c => c.Team.Id, c => c);

        Team selectedClub;

        if (clubsInCup.Count > 0)
        {
            // Pick the weakest club among those still in the cup
            var clubsInCupData = clubsInCup.Select(c => clubDataById[c.Id]).ToList();
            selectedClub = clubsInCupData.OrderBy(c => c.Strength).First().Team;
            _logger.LogInformation("Auto-assigned weakest club in cup: {ClubName}", selectedClub.Name);
        }
        else
        {
            // No clubs in cup: pick the strongest among the weakest available clubs
            // Group clubs by division, pick from the lowest division available
            var lowestDivision = clubData.Max(c => c.Division);
            var clubsInLowestDiv = clubData.Where(c => c.Division == lowestDivision).ToList();
            selectedClub = clubsInLowestDiv.OrderByDescending(c => c.Strength).First().Team;
            _logger.LogInformation("Auto-assigned strongest club from lowest division ({Division}): {ClubName}", 
                lowestDivision, selectedClub.Name);
        }

        return selectedClub;
    }

    /// <summary>
    /// Calculates division and strength for all clubs in a single pass using bulk queries.
    /// </summary>
    private async Task<List<ClubStrengthData>> CalculateAllClubDataAsync(
        IReadOnlyList<Team> clubs, 
        Guid seasonId, 
        CancellationToken cancellationToken)
    {
        var result = new List<ClubStrengthData>();

        // Get all team IDs
        var teamIds = clubs.Select(c => c.Id).ToList();

        // Get divisions for all teams in bulk
        var teamDivisions = await _teams.GetTeamDivisionsAsync(seasonId, teamIds, cancellationToken);

        // Get squads for all teams in bulk
        var allSquads = await _teams.GetSquadsAsync(teamIds, seasonId, cancellationToken);

        // Get all unique player IDs
        var allPlayerIds = allSquads.Values
            .SelectMany(s => s.Select(m => m.PlayerId))
            .Distinct()
            .ToList();
        
        // Get all players in bulk
        var players = await _teams.GetPlayersAsync(allPlayerIds, cancellationToken);

        // Calculate strength for each club
        foreach (var club in clubs)
        {
            var memberships = allSquads.GetValueOrDefault(club.Id, new List<TeamMembership>());
            var clubPlayers = memberships
                .Select(m => players.GetValueOrDefault(m.PlayerId))
                .Where(p => p is not null)
                .Cast<NinjaEleven.Domain.Players.Player>()
                .ToList();

            var strength = NinjaEleven.Domain.Competitions.ClubStrength.Calculate(clubPlayers);
            var division = teamDivisions.GetValueOrDefault(club.Id, 0);

            result.Add(new ClubStrengthData(club, division, strength));
        }

        return result;
    }

    private record ClubStrengthData(Team Team, int Division, double Strength);

    private async Task<Guid?> GetCupEditionForSeasonAsync(Guid seasonId, CancellationToken cancellationToken)
    {
        var views = await _competitions.ListSeasonViewsAsync(seasonId, cancellationToken);
        var cupView = views.FirstOrDefault(v => v.Type == CompetitionType.Cup);
        return cupView?.Id;
    }

    /// <summary>
    /// Signs a user in by email and password, returning the claims needed for a JWT.
    /// </summary>
    public async Task<AuthResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByEmailAsync(email, cancellationToken);
        if (user is null)
            throw new DomainValidationException(
                "InvalidCredentials", "The email or password is incorrect.");

        if (!_passwordHasher.Verify(password, user.PasswordHash))
            throw new DomainValidationException(
                "InvalidCredentials", "The email or password is incorrect.");

        var coachName = user.Manager?.Name;
        var teamId = user.Manager?.TeamId;

        _logger.LogInformation("User logged in: {Email}.", email);

        return new AuthResult(user.Id, user.Email, teamId, coachName);
    }

    /// <summary>
    /// Changes the password of the authenticated user.
    /// </summary>
    public async Task ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetAsync(userId, cancellationToken)
            ?? throw new EntityNotFoundException("User", userId);

        if (!_passwordHasher.Verify(currentPassword, user.PasswordHash))
            throw new DomainValidationException(
                "InvalidCredentials", "The current password is incorrect.");

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            throw new DomainValidationException(
                "WeakPassword", "The new password must be at least six characters long.");

        user.SetPasswordHash(_passwordHasher.Hash(newPassword));
        _users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password changed for user {Email}.", user.Email);
    }

    /// <summary>
    /// The manager linked to a user, if any. Used to return the manager detail after linking.
    /// </summary>
    public async Task<Manager?> GetManagerByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _managers.ListByUserIdAsync(userId, cancellationToken);
}

/// <summary>The result of a login or registration: enough to mint a JWT.</summary>
public record AuthResult(
    Guid UserId,
    string Email,
    Guid? TeamId,
    string? CoachName);