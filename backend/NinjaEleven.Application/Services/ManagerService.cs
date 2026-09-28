using Microsoft.Extensions.Logging;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Managers;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Services;

/// <summary>
/// Creates and looks up the manager who owns the career.
///
/// A career begins when a club is chosen and a name is given to the manager who will run it.
/// From that moment the manager is tied to the club for the whole career: a club has one
/// manager and only one, and the choice cannot be undone, so the change-club door is a door
/// that no longer opens.
/// </summary>
public class ManagerService
{
    private readonly ITeamRepository _teams;
    private readonly IManagerRepository _managers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ManagerService> _logger;

    public ManagerService(
        ITeamRepository teams,
        IManagerRepository managers,
        IUnitOfWork unitOfWork,
        ILogger<ManagerService> logger)
    {
        _teams = teams;
        _managers = managers;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Manager> GetByTeamAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        var manager = await _managers.GetByTeamAsync(teamId, cancellationToken);
        if (manager is null)
            throw new EntityNotFoundException("Manager", teamId);

        return manager;
    }

    /// <summary>
    /// Creates the manager for a club the moment the career begins. This is the one place a
    /// club gets a manager, and the one place it is checked: a second call for the same club
    /// is refused, because a club already managed is not a club to be managed again.
    ///
    /// The club is also marked as the one a person is running, and this is the seam for it. The
    /// mark is what the market reads to decide whose offers wait in an inbox and whose are
    /// settled by a formula, so leaving it to a second call from the client is leaving the
    /// market's idea of whose career this is to a client that may be a build behind. A career
    /// that begins here has a person behind it, and that is exactly the fact the mark records.
    /// </summary>
    public async Task<Manager> CreateAsync(Guid teamId, string name, CancellationToken cancellationToken = default)
    {
        var team = await _teams.GetAsync(teamId, cancellationToken)
            ?? throw new EntityNotFoundException("Team", teamId);

        var existing = await _managers.GetByTeamAsync(teamId, cancellationToken);
        if (existing is not null)
            throw new DomainValidationException(
                "ManagerAlreadyExists",
                $"{team.Name} already has a manager and cannot take another.");

        var manager = Manager.Create(teamId, name);
        await _managers.AddAsync(manager, cancellationToken);

        var marked = await _teams.MarkAsManagerClubAsync(teamId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Career started: {ManagerName} takes charge of {TeamName}.", name, marked.Name);

        return manager;
    }

    /// <summary>
    /// Changes the manager's name. The club stays the same — this is just the name on the
    /// door, not a new career.
    /// </summary>
    public async Task<Manager> RenameAsync(Guid teamId, string name, CancellationToken cancellationToken = default)
    {
        var manager = await _managers.GetByTeamAsync(teamId, cancellationToken)
            ?? throw new EntityNotFoundException("Manager", teamId);

        manager.SetName(name);
        _managers.Update(manager);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Manager of {TeamName} renamed to {ManagerName}.", manager.TeamId, name);

        return manager;
    }
}
