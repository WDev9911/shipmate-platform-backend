using ShipMate.Domain.Common;
using ShipMate.Domain.Constants;

namespace ShipMate.Domain.Entities;

public enum ChecklistEditError { None, ItemNotApplicable, MainJourneyRequired }

// Did this edit finish the list or reopen it? The service writes the audit record only on a change.
public enum ChecklistTransition { None, Completed, Reopened }

public record ChecklistEditResult(ChecklistEditError Error, ChecklistTransition Transition)
{
    public static ChecklistEditResult Fail(ChecklistEditError error) => new(error, ChecklistTransition.None);
}

/// <summary>
/// What the team checks on the running product before one version is shipped. Every MVP snapshot gets its own list,
/// so a version is never shipped on the strength of the previous one's checks.
/// </summary>
public class VerificationChecklist : BaseEntity
{
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;

    public Guid MvpSnapshotId { get; set; }
    public MvpSnapshot MvpSnapshot { get; set; } = null!;

    // Copied from the snapshot: the version is the number the API and the UI speak in.
    public int SnapshotVersion { get; set; }

    // Kept up to date by every edit, so SHIP and NBA read it without working it out again.
    public bool AllItemsChecked { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // Raised by every edit; a save is accepted only if nobody edited since this was read.
    public int Revision { get; private set; }

    public List<VerificationChecklistItem> Items { get; set; } = new();

    // Only the foreign key is set, not the snapshot itself, so adding the list never touches the snapshot's own row.
    public static VerificationChecklist CreateFor(MvpSnapshot snapshot)
    {
        var checklist = new VerificationChecklist
        {
            WorkspaceId = snapshot.WorkspaceId,
            MvpSnapshotId = snapshot.Id,
            SnapshotVersion = snapshot.Version
        };

        foreach (var key in VerificationItemKeys.All)
        {
            checklist.Items.Add(new VerificationChecklistItem { ChecklistId = checklist.Id, ItemKey = key });
        }

        return checklist;
    }

    /// <summary>
    /// Ticks or unticks one check. The key is known: the caller refuses anything else before getting here.
    /// No note sent keeps the old one; a blank note sent clears it.
    /// </summary>
    public ChecklistEditResult Check(string key, bool isChecked, string? note, Guid userId, DateTime now)
    {
        var item = ItemOf(key);

        if (isChecked && !item.Applicable)
        {
            return ChecklistEditResult.Fail(ChecklistEditError.ItemNotApplicable);
        }

        item.Checked = isChecked;
        item.CheckedByUserId = isChecked ? userId : null;
        item.CheckedAt = isChecked ? now : null;

        if (note is not null)
        {
            item.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        }

        return new ChecklistEditResult(ChecklistEditError.None, Recompute(now));
    }

    /// <summary>
    /// Marks a check as not fitting this product, or as fitting again. Either way it has to be ticked afresh:
    /// what was ticked under another rule says nothing now.
    /// </summary>
    public ChecklistEditResult SetApplicability(string key, bool applicable, string? reason, DateTime now)
    {
        // A product always has a main journey, so that check can't be skipped.
        if (!applicable && key == VerificationItemKeys.MainUserJourney)
        {
            return ChecklistEditResult.Fail(ChecklistEditError.MainJourneyRequired);
        }

        var item = ItemOf(key);

        // Saying again what is already so must not wipe a tick.
        if (item.Applicable == applicable)
        {
            if (!applicable)
            {
                item.NotApplicableReason = reason;
            }

            return new ChecklistEditResult(ChecklistEditError.None, Recompute(now));
        }

        item.Applicable = applicable;
        item.NotApplicableReason = applicable ? null : reason;
        item.Checked = false;
        item.CheckedByUserId = null;
        item.CheckedAt = null;

        return new ChecklistEditResult(ChecklistEditError.None, Recompute(now));
    }

    // The checks still to tick, in the order they are shown.
    public IReadOnlyList<string> MissingItems() =>
        Items.Where(i => i.Applicable && !i.Checked)
            .Select(i => i.ItemKey)
            .OrderBy(key => VerificationItemKeys.All.ToList().IndexOf(key))
            .ToList();

    private VerificationChecklistItem ItemOf(string key) => Items.Single(i => i.ItemKey == key);

    // The one place where the summary fields are worked out.
    private ChecklistTransition Recompute(DateTime now)
    {
        var all = Items.All(i => !i.Applicable || i.Checked);

        var transition = ChecklistTransition.None;
        if (all && !AllItemsChecked)
        {
            CompletedAt = now;
            transition = ChecklistTransition.Completed;
        }
        else if (!all && AllItemsChecked)
        {
            CompletedAt = null;
            transition = ChecklistTransition.Reopened;
        }

        AllItemsChecked = all;
        UpdatedAt = now;
        Revision++;

        return transition;
    }
}
