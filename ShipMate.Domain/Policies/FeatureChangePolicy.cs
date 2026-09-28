using ShipMate.Domain.Enums;

namespace ShipMate.Domain.Policies;

/// <summary>
/// DEFINE steps 5, 7 and 8: which changes the developer may make directly. Everything is free except
/// editing or excluding a committed feature, which must go through the customer-confirmed change request (step 8).
/// </summary>
public static class FeatureChangePolicy
{
    public static bool RequiresChangeRequestForStatus(FeatureOrigin origin, FeatureStatus targetStatus) =>
        origin == FeatureOrigin.FromCommittedList && targetStatus == FeatureStatus.Excluded;

    public static bool RequiresChangeRequestForEdit(FeatureOrigin origin) =>
        origin == FeatureOrigin.FromCommittedList;
}
