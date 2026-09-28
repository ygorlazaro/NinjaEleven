namespace NinjaEleven.Application.Abstractions;

/// <summary>
/// Hashes and verifies passwords. The one implementation in production uses BCrypt; a test
/// double can substitute it so the game never holds a real password hash in memory.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}
