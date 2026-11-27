using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.Admin;

public class AdminActionLogQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public AdminAction? Action { get; set; }
    public Guid? AdminUserId { get; set; }
}
