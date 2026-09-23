using ShipMate.Domain.Entities;

namespace ShipMate.Application.Interfaces.Repositories;

public interface IGitHubConnectionRepository
{
    Task<GitHubConnection?> GetByUserIdAsync(Guid userId);
    Task AddAsync(GitHubConnection connection);
    void Update(GitHubConnection connection);
    void Remove(GitHubConnection connection);
}
