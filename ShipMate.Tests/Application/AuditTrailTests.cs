using ShipMate.Application.Services;
using ShipMate.Domain.Common;
using ShipMate.Domain.Policies;

namespace ShipMate.Tests.Application;

public class AuditTrailTests
{
    private readonly InMemoryAuditTrailRepository _repository = new();
    private readonly AuditTrail _audit;
    private readonly Guid _workspace = Guid.NewGuid();

    public AuditTrailTests()
    {
        _audit = new AuditTrail(_repository);
    }

    [Fact]
    public async Task Records_ChainOntoEachOtherInOrder()
    {
        await _audit.AppendAsync(_workspace, AuditTypes.SnapshotLocked, Guid.NewGuid(), new { version = 1 });
        await _audit.AppendAsync(_workspace, AuditTypes.ScopeChange, Guid.NewGuid(), new { version = 2 });
        await _audit.AppendAsync(_workspace, AuditTypes.ScopeBypass, null, new { alert = "x" });

        Assert.Equal([1L, 2L, 3L], _repository.Entries.Select(e => e.Sequence));
        Assert.Equal(_repository.Entries[0].RecordHash, _repository.Entries[1].PrevHash);
        Assert.Null(await _audit.FindFirstBrokenAsync(_workspace));
    }

    [Fact]
    public async Task EachWorkspace_HasItsOwnChain()
    {
        var other = Guid.NewGuid();

        await _audit.AppendAsync(_workspace, AuditTypes.SnapshotLocked, null, new { });
        await _audit.AppendAsync(other, AuditTypes.SnapshotLocked, null, new { });

        Assert.All(_repository.Entries, e => Assert.Equal(1, e.Sequence));
        Assert.All(_repository.Entries, e => Assert.Equal(AuditChain.GenesisHash, e.PrevHash));
    }

    [Fact]
    public async Task ThePayload_IsStoredAsTheExactTextThatWasHashed()
    {
        await _audit.AppendAsync(_workspace, AuditTypes.ScopeBypass, null, new { alertId = "a1", reason = "because" });

        Assert.Equal("{\"alertId\":\"a1\",\"reason\":\"because\"}", _repository.Entries[0].Payload);
    }

    [Fact]
    public async Task ATamperedRecord_IsFoundByVerification()
    {
        for (var i = 0; i < 4; i++)
        {
            await _audit.AppendAsync(_workspace, AuditTypes.ScopeBypass, null, new { i });
        }

        _repository.Entries[1].Payload = "{\"i\":42}";

        Assert.Equal(2, await _audit.FindFirstBrokenAsync(_workspace));
    }

    [Fact]
    public async Task AWorkspaceWithoutRecords_IsIntact()
    {
        Assert.Null(await _audit.FindFirstBrokenAsync(_workspace));
    }
}
