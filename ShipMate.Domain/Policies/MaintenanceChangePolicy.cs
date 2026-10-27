using ShipMate.Domain.ValueObjects;

namespace ShipMate.Domain.Policies;

/// <summary>
/// BUILD Rule Engine (layer 1): when new commits are only maintenance, the AI is skipped. A commit message alone never
/// qualifies: the diff must also contain no executable code, so a "refactor" message can't hide new logic.
/// </summary>
public static class MaintenanceChangePolicy
{
    private static readonly string[] MaintenanceMessages = ["fix typo", "refactor", "bump version", "format code"];
    private static readonly string[] CommentStarts = ["//", "/*", "*/", "<!--"];
    // These start real code too (#include, *ptr, --counter), so only count when a space or the end follows.
    private static readonly string[] SpacedCommentMarks = ["#", "*", "--"];

    public static bool IsMaintenanceOnly(IReadOnlyCollection<string> commitMessages, IReadOnlyCollection<ChangedFile> files) =>
        commitMessages.Count > 0
        && commitMessages.All(IsMaintenanceMessage)
        && files.All(HasNoLogicChange);

    private static bool IsMaintenanceMessage(string message) =>
        MaintenanceMessages.Any(pattern => message.Contains(pattern, StringComparison.OrdinalIgnoreCase));

    private static bool HasNoLogicChange(ChangedFile file) =>
        IsConfigOrDocPath(file.Path) || (file.Patch is not null && IsOnlyWhitespaceOrComments(file.Patch));

    private static bool IsComment(string content) =>
        CommentStarts.Any(start => content.StartsWith(start, StringComparison.Ordinal))
        || SpacedCommentMarks.Any(mark =>
            content == mark || content.StartsWith(mark + " ", StringComparison.Ordinal));

    private static bool IsConfigOrDocPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        var name = normalized[(normalized.LastIndexOf('/') + 1)..];

        return name == ".gitignore"
            || name.StartsWith("README", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("LICENSE", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("docs/", StringComparison.OrdinalIgnoreCase);
    }

    // Blank lines and comment lines are ignored; what remains must read the same before and after once whitespace
    // is removed, so re-indenting or reflowing code passes but changing a token doesn't.
    private static bool IsOnlyWhitespaceOrComments(string patch)
    {
        var removed = new System.Text.StringBuilder();
        var added = new System.Text.StringBuilder();

        foreach (var line in patch.Split('\n'))
        {
            if (line.Length == 0 || line[0] is not ('+' or '-'))
            {
                continue;
            }

            var content = line[1..].Trim();
            if (content.Length == 0 || IsComment(content))
            {
                continue;
            }

            (line[0] == '-' ? removed : added).Append(string.Concat(content.Where(c => !char.IsWhiteSpace(c))));
        }

        return removed.ToString() == added.ToString();
    }
}
