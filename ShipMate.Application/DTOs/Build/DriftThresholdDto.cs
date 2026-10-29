namespace ShipMate.Application.DTOs.Build;

public class ChangeDriftThresholdRequest
{
    public decimal Value { get; set; }
}

public class DriftThresholdDto
{
    public decimal Value { get; set; }

    // How many of the project's changes are used, out of how many it has.
    public int ChangesUsed { get; set; }
    public int MaxChanges { get; set; }

    // When the next change is allowed; null once no change is left.
    public DateTime? NextChangeAllowedAt { get; set; }
}

public class DriftThresholdHistoryDto : DriftThresholdDto
{
    // Oldest first.
    public List<DriftThresholdChangeDto> History { get; set; } = [];
}

public class DriftThresholdChangeDto
{
    public int ChangeNumber { get; set; }
    public decimal From { get; set; }
    public decimal To { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public DateTime ChangedAt { get; set; }
}
