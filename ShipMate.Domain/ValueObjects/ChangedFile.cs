namespace ShipMate.Domain.ValueObjects;

/// <summary>One file of a Git diff. Patch is null for binary files and files too large for GitHub to show.</summary>
/// <param name="Changes">Lines added plus removed. GitHub leaves the patch out of a file whose diff is too large to show,
/// so a missing patch with changes means "too big", while a missing patch with no changes is a binary file.</param>
public record ChangedFile(string Path, string Status, string? Patch, int Changes = 0);
