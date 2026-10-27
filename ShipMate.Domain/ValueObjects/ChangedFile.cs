namespace ShipMate.Domain.ValueObjects;

/// <summary>One file of a Git diff. Patch is null for binary files and files too large for GitHub to show.</summary>
public record ChangedFile(string Path, string Status, string? Patch);
