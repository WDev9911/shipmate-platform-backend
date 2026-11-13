namespace ShipMate.Application.DTOs.Verify;

public class CheckVerificationItemRequest
{
    public bool Checked { get; set; }

    // Optional: a line, or a link, saying what was checked. Not sent keeps the old note; blank clears it.
    public string? Note { get; set; }
}

public class SetVerificationItemApplicabilityRequest
{
    public bool Applicable { get; set; }

    // Required when skipping a check, so the next reader knows why.
    public string? Reason { get; set; }
}
