using ShipMate.Domain.Entities;

namespace ShipMate.Application.Interfaces.Repositories;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByGitHubIdAsync(string githubId);
    Task AddAsync(User user);
    void Update(User user);
    Task<bool> SaveChangesAsync();
}
