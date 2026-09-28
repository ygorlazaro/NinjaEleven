namespace NinjaEleven.Api.Contracts;

/// <summary>Returned by login and register: the JWT and the identity it carries.</summary>
public class AuthResponseDto
{
    public string Token { get; init; } = string.Empty;
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public Guid? TeamId { get; init; }
    public string? CoachName { get; init; }
}

public class AuthRegisterRequestDto
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string? CoachName { get; init; }
    public Guid? TeamId { get; init; }
}

public class AuthLoginRequestDto
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public class ChangePasswordRequestDto
{
    public string CurrentPassword { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
}

/// <summary>Links the authenticated user to a newly created manager.</summary>
public class LinkManagerRequestDto
{
    public Guid TeamId { get; init; }
    public string CoachName { get; init; } = string.Empty;
}
