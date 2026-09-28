namespace ShipMate.Application.DTOs.ProductDefinitions;

public class AnalyzeProductDefinitionRequest
{
    // The developer's own request to the AI for this run, e.g. "drop everything about payments".
    public string? Instruction { get; set; }

    // "The persona needs fixing": choose the persona again from all current features (DEFINE step 5).
    public bool ReassessPersona { get; set; }
}
