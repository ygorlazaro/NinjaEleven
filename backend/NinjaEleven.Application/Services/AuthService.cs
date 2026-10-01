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
    private readonly IClock _clock;
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
        IClock clock,
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
        _clock = clock;
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
        var assignedTeam = await AutoAssignClubAsync(null, cancellationToken);

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
    /// Automatically assigns a club, following the current rule.
    /// </summary>
    /// <param name="notThisClub">
    /// A club the account must not be given. A manager who was dismissed is owed a club, and
    /// the one rule the reactivation cannot bend is that it is not the club they just lost:
    /// handing it straight back would answer "you were fired" with the same job, and the
    /// dismissal would be a piece of bookkeeping rather than an event.
    /// </param>
    /// <param name="cancellationToken">Cancellation.</param>
    private async Task<Team> AutoAssignClubAsync(
        Guid? notThisClub,
        CancellationToken cancellationToken)
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

        // The club just lost is offered back only if it is the last club on earth. Excluding it
        // is what makes a dismissal mean something, but a world that has run out of other
        // clubs cannot honour "a different one", and refusing the sign-in over it would lock a
        // person out of a game they have an account for. So the exclusion is tried first and
        // the whole pool is the answer when the first one is empty.
        if (notThisClub is Guid previous)
        {
            var withoutTheOldClub = clubsWithoutManager.Where(club => club.Id != previous).ToList();

            if (withoutTheOldClub.Count > 0)
            {
                _logger.LogInformation(
                    "A club is being chosen for a returning manager; the one they were dismissed from is not offered back.");
                clubsWithoutManager = withoutTheOldClub;
            }
            else
            {
                _logger.LogWarning(
                    "Every other club in the world already has a manager, so the club the returning manager was dismissed from is being offered back rather than leaving them with none.");
            }
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
    ///
    /// Signing in is what tells the world a manager is still here, so this is the one place
    /// the sign-in is recorded — and a manager whose account was dismissed for going quiet is
    /// given a club again on the way in, because a person who came back has answered the only
    /// question the dismissal asked.
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

        var wasDismissed = !user.IsActive;

        // Read before the reactivation clears it: the log line that says a returning manager
        // was dismissed on some day must not read a day that has just been erased.
        var dismissedOn = user.DismissedAt;

        // The club the account was dismissed from. It is read off the account rather than off
        // the manager link because a dismissal is the act of clearing that link — the row on
        // the other side no longer points at anybody, so a navigation built on it answers
        // nothing at all, and the reactivation would be free to hand back the very club the
        // person was just dismissed from.
        var previousClubId = user.DismissedTeamId ?? user.Manager?.TeamId;

        if (wasDismissed)
        {
            await ReactivateAsync(user, previousClubId, dismissedOn, cancellationToken);
        }
        else
        {
            user.RecordLogin(_clock.UtcNow);
        }

        var coachName = user.Manager?.Name;
        var teamId = user.Manager?.TeamId;

        _logger.LogInformation(
            "User logged in: {Email}.{Return}",
            email,
            wasDismissed
                ? $" Dismissed on {dismissedOn:yyyy-MM-dd}, so this is a return: they have been given a club again."
                : string.Empty);

        return new AuthResult(user.Id, user.Email, teamId, coachName, wasDismissed);
    }

    /// <summary>
    /// Gives a dismissed account its life and a club again.
    ///
    /// The club is chosen by the same rule that gives a new account one, because there is
    /// only one rule about who runs which club and a returning manager is not a special case
    /// of a manager. The old club is the single exception and it is excluded by the caller.
    /// </summary>
    private async Task ReactivateAsync(
        User user,
        Guid? previousClubId,
        DateTimeOffset? dismissedOn,
        CancellationToken cancellationToken)
    {
        user.Reactivate(_clock.UtcNow);

        // The name the manager chose is not on the account — it is on the manager row, and a
        // dismissal is the act of clearing that row's link to the account, so the name is read
        // off the club they were let go from before anything else happens. Reading it after
        // would hand the person a new career under a name they never picked.
        var coachName = previousClubId is Guid previous
            ? (await _managers.GetByTeamAsync(previous, cancellationToken))?.Name
            : null;

        Team assignedTeam;
        try
        {
            assignedTeam = await AutoAssignClubAsync(previousClubId, cancellationToken);
        }
        catch (DomainValidationException refused) when (refused.Code == "NoAvailableClubs")
        {
            // A world where every club already has a person in it cannot also give this one
            // back. The account is alive and holds nothing, which is exactly what an account
            // with no club is, and the StartScreen is where a person without one is sent.
            _logger.LogWarning(
                "Dismissed account {Email} signed in again but no club is free, so it has been reactivated without one.",
                user.Email);

            _users.Update(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        // The club chosen is somebody else's NPC chair, and a club has one manager: the NPC
        // steps aside for the person, the same way it does the day an account is created.
        var existingManager = await _managers.GetByTeamAsync(assignedTeam.Id, cancellationToken);
        if (existingManager is not null)
        {
            _managers.Remove(existingManager);
        }

        var manager = Manager.Create(assignedTeam.Id, coachName ?? user.Manager?.Name ?? "Técnico");
        manager.SetUserId(user.Id);
        user.SetManager(manager);

        await _teams.MarkAsManagerClubAsync(assignedTeam.Id, cancellationToken);
        await _managers.AddAsync(manager, cancellationToken);
        _users.Update(user);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Account {Email} was dismissed on {DismissedOn:yyyy-MM-dd} and has come back; it now runs {ClubName}.",
            user.Email,
            dismissedOn,
            assignedTeam.Name);
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
/// <param name="WasDismissed">
/// True when this sign-in brought a dismissed account back. The account is alive and holds a
/// club, but the club is not the one it had, and a client that kept the old club in a store
/// has to be told rather than left to discover it on a screen that no longer loads.
/// </param>
public record AuthResult(
    Guid UserId,
    string Email,
    Guid? TeamId,
    string? CoachName,
    bool WasDismissed = false);
