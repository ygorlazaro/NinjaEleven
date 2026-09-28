using NinjaEleven.Domain.Users;

namespace NinjaEleven.Application.Repositories;

/// <summary>
/// Persistence for user accounts. A user is digital identity — email and password hash —
/// and the link to the manager they have created, if any.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    void Update(User user);
}
