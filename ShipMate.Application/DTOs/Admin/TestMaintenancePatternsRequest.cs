namespace ShipMate.Application.DTOs.Admin;

/// <summary>Tries a commit against a list of patterns without saving anything.</summary>
public class TestMaintenancePatternsRequest
{
    /// <summary>The patterns to try; the saved ones when left out.</summary>
    public List<string>? Patterns { get; set; }

    public List<string> CommitMessages { get; set; } = [];

    /// <summary>The files of the commit. When given, the answer also says whether BUILD would skip the AI.</summary>
    public List<TestChangedFileDto>? Files { get; set; }
}

public class TestChangedFileDto
{
    public string Path { get; set; } = string.Empty;
    public string? Patch { get; set; }
}

public class TestMaintenancePatternsResponse
{
    public List<TestedMessageDto> Messages { get; set; } = [];

    /// <summary>Null when no files were given. True when every message matches and every file is config, documentation or only whitespace and comments.</summary>
    public bool? SkippedByRuleEngine { get; set; }
}

public class TestedMessageDto
{
    public string Message { get; set; } = string.Empty;
    public bool Matches { get; set; }
}
