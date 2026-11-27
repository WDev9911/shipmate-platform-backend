using ShipMate.Application.DTOs.Admin;
using ShipMate.Domain.Entities;

namespace ShipMate.Application.Interfaces.Repositories;

public interface IAdminActionLogRepository
{
    Task AddAsync(AdminActionLog log);
    Task<List<AdminActionLog>> GetByTargetUserIdAsync(Guid targetUserId);

    /// <summary>Newest first, with the admin and the target account loaded.</summary>
    Task<(List<AdminActionLog> Items, int TotalCount)> GetPagedAsync(AdminActionLogQuery query);
}
