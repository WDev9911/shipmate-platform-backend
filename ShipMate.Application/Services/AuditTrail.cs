using System.Text.Json;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Policies;

namespace ShipMate.Application.Services;

public class AuditTrail : IAuditTrail
{
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    private readonly IAuditTrailRepository _repository;

    public AuditTrail(IAuditTrailRepository repository)
    {
        _repository = repository;
    }

    public async Task AppendAsync(Guid workspaceId, string auditType, Guid? actorUserId, object payload)
    {
        var entry = new AuditTrailEntry
        {
            WorkspaceId = workspaceId,
            AuditType = auditType,
            ActorUserId = actorUserId,
            Payload = JsonSerializer.Serialize(payload, PayloadOptions)
        };

        AuditChain.Link(entry, await _repository.GetLastAsync(workspaceId));
        _repository.Add(entry);
    }

    public async Task<long?> FindFirstBrokenAsync(Guid workspaceId) =>
        AuditChain.FindFirstBroken(await _repository.GetChainAsync(workspaceId));
}
