using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.Admin;

/// <summary>Filters of the user list. Bound as one object so its validator runs.</summary>
public class AdminUserQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>Part of the email or display name.</summary>
    public string? Search { get; set; }

    public UserRole? Role { get; set; }
    public UserStatus? Status { get; set; }
}
