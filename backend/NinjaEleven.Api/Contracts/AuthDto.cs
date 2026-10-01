namespace NinjaEleven.Api.Contracts;

/// <summary>Returned by login and register: the JWT and the identity it carries.</summary>
public class AuthResponseDto
{
    public string Token { get; init; } = string.Empty;
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public Guid? TeamId { get; init; }
    public string? CoachName { get; init; }

    /// <summary>
    /// True when this sign-in brought a dismissed account back. The club in <see cref="TeamId"/>
    /// is a different one from the club the account had, so a client holding the old club has
    /// to drop it rather than keep a screen the backend no longer agrees with.
    /// </summary>
    public bool WasDismissed { get; init; }
}

public class AuthRegisterRequestDto
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string? CoachName { get; init; }
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
