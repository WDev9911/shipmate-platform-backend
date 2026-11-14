namespace ShipMate.Application.DTOs.Verify;

public class VerificationItemDto
{
    public string Key { get; set; } = string.Empty;
    public bool Applicable { get; set; }
    public string? NotApplicableReason { get; set; }
    public bool Checked { get; set; }
    public string? Note { get; set; }
    public Guid? CheckedByUserId { get; set; }
    public DateTime? CheckedAt { get; set; }
}

/// <summary>Facts shown next to the checklist. None of them blocks anything.</summary>
public class VerificationInfoDto
{
    // Open drift alerts on the features this version has locked.
    public int OpenDriftCount { get; set; }

    // Locked features whose newest submission was approved, out of all the locked ones.
    public int ApprovedFeatures { get; set; }
    public int LockedFeatures { get; set; }
}

public class VerificationChecklistDto
{
    public int Version { get; set; }
    public bool AllItemsChecked { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<string> MissingItems { get; set; } = new();
    public List<VerificationItemDto> Items { get; set; } = new();
    public VerificationInfoDto Info { get; set; } = new();
}

public class VerificationChecklistSummaryDto
{
    public int Version { get; set; }
    public bool IsLatest { get; set; }
    public bool AllItemsChecked { get; set; }
    public int MissingCount { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>What SHIP and NBA need to know about one version's checklist, without reading its tables.</summary>
public record VerificationReadiness(
    bool Exists,
    bool AllItemsChecked,
    IReadOnlyList<string> MissingItems,
    DateTime? CompletedAt,
    Guid? ChecklistId)
{
    // A version without a checklist can't be shipped.
    public static readonly VerificationReadiness Missing = new(false, false, [], null, null);
}
