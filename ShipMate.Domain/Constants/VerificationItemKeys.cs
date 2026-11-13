namespace ShipMate.Domain.Constants;

/// <summary>
/// The checks a team makes on the running product before a version is shipped. The values are stored and returned as they are.
/// </summary>
public static class VerificationItemKeys
{
    // The main user journey works from start to end.
    public const string MainUserJourney = "main_user_journey";

    // Sign in, sign out and permissions.
    public const string Authentication = "authentication";

    // Production environment variables and secrets are set, and set right.
    public const string ProductionConfig = "production_config";

    // Create, read, update and delete work on the main data.
    public const string BasicCrud = "basic_crud";

    // An error doesn't leave a blank page.
    public const string ErrorHandling = "error_handling";

    // The order the list is shown in.
    public static readonly IReadOnlyList<string> All =
        [MainUserJourney, Authentication, ProductionConfig, BasicCrud, ErrorHandling];

    public static bool IsKnown(string key) => All.Contains(key);
}
