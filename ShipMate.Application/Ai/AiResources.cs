namespace ShipMate.Application.Ai;

/// <summary>Reads prompt and schema files embedded in this assembly (see the csproj), by file name.</summary>
public static class AiResources
{
    public static string ReadText(string fileName)
    {
        using var stream = typeof(AiResources).Assembly.GetManifestResourceStream(fileName)
            ?? throw new InvalidOperationException($"Embedded AI resource '{fileName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
