namespace ShipMate.Application.Exceptions;

public class VerificationItemUnknownException : AppException
{
    public override string ErrorCode => "VERIFY_ITEM_UNKNOWN";
    public override int StatusCode => 400;

    public VerificationItemUnknownException()
        : base("That is not one of the checks of the Verification Checklist.")
    {
    }
}

public class VerificationItemNotApplicableException : AppException
{
    public override string ErrorCode => "VERIFY_ITEM_NOT_APPLICABLE";
    public override int StatusCode => 409;

    public VerificationItemNotApplicableException()
        : base("This check was marked as not applicable. Mark it applicable again before ticking it.")
    {
    }
}

public class VerificationMainJourneyRequiredException : AppException
{
    public override string ErrorCode => "VERIFY_MAIN_JOURNEY_REQUIRED";
    public override int StatusCode => 409;

    public VerificationMainJourneyRequiredException()
        : base("The main user journey can't be skipped: every product has one.")
    {
    }
}

public class VerificationChecklistConflictException : AppException
{
    public override string ErrorCode => "VERIFY_CHECKLIST_CONFLICT";
    public override int StatusCode => 409;

    public VerificationChecklistConflictException()
        : base("Someone else changed this checklist at the same time. Reload it and try again.")
    {
    }
}
