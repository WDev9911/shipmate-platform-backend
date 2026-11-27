using ShipMate.Domain.Entities;

namespace ShipMate.Application.Interfaces.Repositories;

public interface IPlatformSettingsRepository
{
    /// <summary>
    /// The one settings row. Throws when it is missing: a made-up row of defaults would quietly undo what an
    /// administrator had set.
    /// </summary>
    Task<PlatformSettings> GetAsync();

    Task<bool> SaveChangesAsync();
}
