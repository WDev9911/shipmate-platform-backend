using ShipMate.Domain.Entities;
using ShipMate.Domain.Policies;

namespace ShipMate.Tests.Domain;

public class AuditChainTests
{
    private readonly Guid _workspace = Guid.NewGuid();

    private AuditTrailEntry Record(AuditTrailEntry? last, string type = "SCOPE_BYPASS", string payload = "{\"a\":1}")
    {
        var entry = new AuditTrailEntry
        {
            WorkspaceId = _workspace, AuditType = type, ActorUserId = Guid.NewGuid(), Payload = payload
        };
        AuditChain.Link(entry, last);
        return entry;
    }

    private List<AuditTrailEntry> Chain(int length)
    {
        var chain = new List<AuditTrailEntry>();
        for (var i = 0; i < length; i++)
        {
            chain.Add(Record(chain.LastOrDefault(), payload: $"{{\"n\":{i}}}"));
        }

        return chain;
    }

    [Fact]
    public void TheFirstRecord_StartsAtOneAndPointsAtTheGenesisHash()
    {
        var first = Record(null);

        Assert.Equal(1, first.Sequence);
        Assert.Equal(AuditChain.GenesisHash, first.PrevHash);
        Assert.Equal(64, first.RecordHash.Length);
    }

    [Fact]
    public void ALaterRecord_FollowsAndPointsAtThePreviousHash()
    {
        var first = Record(null);
        var second = Record(first);

        Assert.Equal(2, second.Sequence);
        Assert.Equal(first.RecordHash, second.PrevHash);
        Assert.NotEqual(first.RecordHash, second.RecordHash);
    }

    [Fact]
    public void LinkingKeepsOnlyTheMicrosecondsADatabaseCanStore()
    {
        var entry = new AuditTrailEntry { CreatedAt = new DateTime(2026, 10, 23, 8, 0, 0, DateTimeKind.Utc).AddTicks(1234567) };

        AuditChain.Link(entry, null);

        Assert.Equal(0, entry.CreatedAt.Ticks % 10);
    }

    [Fact]
    public void AnIntactChain_HasNoBrokenRecord()
    {
        Assert.Null(AuditChain.FindFirstBroken(Chain(5)));
    }

    [Fact]
    public void AnEmptyChain_IsIntact()
    {
        Assert.Null(AuditChain.FindFirstBroken([]));
    }

    [Fact]
    public void ChangingAPayload_IsReportedAtThatRecord()
    {
        var chain = Chain(5);
        chain[2].Payload = "{\"n\":999}";

        Assert.Equal(3, AuditChain.FindFirstBroken(chain));
    }

    [Fact]
    public void ChangingTheActor_IsReportedAtThatRecord()
    {
        var chain = Chain(4);
        chain[1].ActorUserId = Guid.NewGuid();

        Assert.Equal(2, AuditChain.FindFirstBroken(chain));
    }

    [Fact]
    public void ChangingATimestamp_IsReportedAtThatRecord()
    {
        var chain = Chain(4);
        chain[3].CreatedAt = chain[3].CreatedAt.AddSeconds(1);

        Assert.Equal(4, AuditChain.FindFirstBroken(chain));
    }

    [Fact]
    public void RehashingATamperedRecord_StillBreaksTheNextOne()
    {
        var chain = Chain(5);
        chain[1].Payload = "{\"n\":999}";
        chain[1].RecordHash = AuditChain.Compute(chain[1]);

        Assert.Equal(3, AuditChain.FindFirstBroken(chain));
    }

    [Fact]
    public void ARemovedRecordInTheMiddle_IsReportedWhereTheGapIs()
    {
        var chain = Chain(5);
        chain.RemoveAt(2);

        Assert.Equal(3, AuditChain.FindFirstBroken(chain));
    }

    [Fact]
    public void ARemovedFirstRecord_IsReported()
    {
        var chain = Chain(3);
        chain.RemoveAt(0);

        Assert.Equal(1, AuditChain.FindFirstBroken(chain));
    }

    [Fact]
    public void RelinkingOntoAnotherTail_GivesAChainThatStillVerifies()
    {
        // What a lost race does: the pending record is moved on top of the record the other writer saved first.
        var chain = Chain(3);
        var pending = Record(chain[1], type: "SCOPE_CHANGE");

        AuditChain.Link(pending, chain[2]);
        chain.Add(pending);

        Assert.Equal(4, pending.Sequence);
        Assert.Null(AuditChain.FindFirstBroken(chain));
    }
}
