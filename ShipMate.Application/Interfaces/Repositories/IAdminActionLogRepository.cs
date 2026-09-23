using ShipMate.Domain.Entities;

namespace ShipMate.Application.Interfaces.Repositories;

public interface IAdminActionLogRepository
{
    Task AddAsync(AdminActionLog log);
    Task<List<AdminActionLog>> GetByTargetUserIdAsync(Guid targetUserId);
}
