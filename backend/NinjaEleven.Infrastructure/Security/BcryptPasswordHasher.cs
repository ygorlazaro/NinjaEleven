using BCrypt.Net;
using NinjaEleven.Application.Abstractions;

namespace NinjaEleven.Infrastructure.Security;

/// <summary>
/// BCrypt implementation of the password hasher. BCrypt embeds the salt in the hash string,
/// so only one column is needed and the verification is self-contained.
/// </summary>
public class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public bool Verify(string password, string passwordHash) =>
        BCrypt.Net.BCrypt.Verify(password, passwordHash);
}
