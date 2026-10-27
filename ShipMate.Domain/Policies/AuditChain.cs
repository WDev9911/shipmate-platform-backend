using System.Security.Cryptography;
using System.Text;
using ShipMate.Domain.Entities;

namespace ShipMate.Domain.Policies;

/// <summary>Links audit records into a hash chain and checks that a stored chain is still intact.</summary>
public static class AuditChain
{
    // What the first record of a workspace points back to.
    public static readonly string GenesisHash = new('0', 64);

    /// <summary>Sets the record's place in the chain: it follows <paramref name="last"/>, or starts the chain.</summary>
    public static void Link(AuditTrailEntry entry, AuditTrailEntry? last)
    {
        // The database keeps microseconds; hashing more than it can give back would fail every check.
        entry.CreatedAt = new DateTime(entry.CreatedAt.Ticks - entry.CreatedAt.Ticks % 10, DateTimeKind.Utc);
        entry.Sequence = (last?.Sequence ?? 0) + 1;
        entry.PrevHash = last?.RecordHash ?? GenesisHash;
        entry.RecordHash = Compute(entry);
    }

    public static string Compute(AuditTrailEntry entry)
    {
        var text = string.Join('|',
            entry.PrevHash,
            entry.Sequence,
            entry.AuditType,
            entry.ActorUserId?.ToString() ?? string.Empty,
            entry.CreatedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ"),
            entry.Payload);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    /// <summary>
    /// Walks the whole chain of a workspace, oldest first. Returns the sequence of the first record that doesn't hold
    /// up (altered, out of place, or a gap where a record was removed); null when the chain is intact.
    /// </summary>
    public static long? FindFirstBroken(IEnumerable<AuditTrailEntry> orderedChain)
    {
        var expectedSequence = 1L;
        var expectedPrevHash = GenesisHash;

        foreach (var entry in orderedChain)
        {
            if (entry.Sequence != expectedSequence
                || entry.PrevHash != expectedPrevHash
                || entry.RecordHash != Compute(entry))
            {
                return expectedSequence;
            }

            expectedSequence++;
            expectedPrevHash = entry.RecordHash;
        }

        return null;
    }
}
