using ShipMate.Domain.Entities;

namespace ShipMate.Application.Interfaces.Services;

/// <summary>
/// Stores one AI call log on its own, apart from the work that made the call. Never throws: a log that can't be
/// written must not fail the call it describes.
/// </summary>
public interface IAiCallLogWriter
{
    Task WriteAsync(AiCallLog log);
}
