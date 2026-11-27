using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Domain.Entities;
using ShipMate.Infrastructure.Persistence;

namespace ShipMate.Infrastructure.Services.Ai;

/// <summary>
/// Saves an AI call log through its own scope and DbContext, never the caller's: the caller may fail before it saves,
/// hit a concurrency conflict, or be in the middle of unrelated changes that must not be saved along with the log.
/// </summary>
public class AiCallLogWriter : IAiCallLogWriter
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AiCallLogWriter> _logger;

    public AiCallLogWriter(IServiceScopeFactory scopeFactory, ILogger<AiCallLogWriter> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task WriteAsync(AiCallLog log)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.AiCallLogs.Add(log);
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // The call it describes already happened; losing its log line must not undo or fail it.
            _logger.LogWarning(ex, "Could not save the log of an AI call ({Purpose}, {Outcome}).", log.Purpose, log.Outcome);
        }
    }
}
