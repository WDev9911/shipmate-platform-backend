namespace ShipMate.Domain.Entities;

/// <summary>One check of a <see cref="VerificationChecklist"/>. Identified by its list and its key.</summary>
public class VerificationChecklistItem
{
    public Guid ChecklistId { get; set; }
    public string ItemKey { get; set; } = string.Empty;

    // A team may skip a check that doesn't fit its product; the reason is kept with it.
    public bool Applicable { get; set; } = true;
    public string? NotApplicableReason { get; set; }

    public bool Checked { get; set; }
    public string? Note { get; set; }
    public Guid? CheckedByUserId { get; set; }
    public DateTime? CheckedAt { get; set; }
}
