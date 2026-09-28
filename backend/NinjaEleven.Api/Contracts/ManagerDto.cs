using NinjaEleven.Domain.Managers;

namespace NinjaEleven.Api.Contracts;

/// <summary>
/// The manager of a club: the name the career was started with, and the club and day it began.
/// </summary>
public class ManagerDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public Guid TeamId { get; init; }
    public Guid? UserId { get; init; }
    public DateTimeOffset StartedAt { get; init; }
}

public static class ManagerDtoMapper
{
    public static ManagerDto ToDto(this Domain.Managers.Manager manager) => new()
    {
        Id = manager.Id,
        Name = manager.Name,
        TeamId = manager.TeamId,
        UserId = manager.UserId,
        StartedAt = manager.StartedAt
    };
}

/// <summary>
/// A request to create a manager and start the career. The name is the only field a manager
/// is born with: the club comes from the route and the start date from the clock.
/// </summary>
public class ManagerCreateRequestDto
{
    public string Name { get; init; } = string.Empty;
}

/// <summary>
/// A request to rename the manager of a club. The club is addressed in the route so a
/// rename is a PUT to the team it belongs to, not to the manager directly.
/// </summary>
public class ManagerRenameRequestDto
{
    public string Name { get; init; } = string.Empty;
}
