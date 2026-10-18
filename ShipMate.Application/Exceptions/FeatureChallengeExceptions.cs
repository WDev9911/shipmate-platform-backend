using ShipMate.Application.DTOs.ProductDefinitions;

namespace ShipMate.Application.Exceptions;

public class ChallengeAlreadyDecidedException : AppException
{
    public override string ErrorCode => "CHALLENGE_ALREADY_DECIDED";
    public override int StatusCode => 409;

    public ChallengeAlreadyDecidedException()
        : base("This challenge was already approved or rejected.")
    {
    }
}

public class ChallengeComplexityNotConfirmedException : AppException
{
    public override string ErrorCode => "CHALLENGE_COMPLEXITY_NOT_CONFIRMED";
    public override int StatusCode => 409;

    public ChallengeComplexityNotConfirmedException()
        : base("Confirm the complexity of the new feature before the challenge can be decided.")
    {
    }
}

public class CommittedRemovalNotConfirmedException : AppException
{
    public override string ErrorCode => "COMMITTED_REMOVAL_NOT_CONFIRMED";
    public override int StatusCode => 409;

    public CommittedRemovalNotConfirmedException()
        : base("The feature to remove came from the committed list. Confirm explicitly that it may be removed.")
    {
    }
}

public class BackupNotApplicableException : AppException
{
    public override string ErrorCode => "BACKUP_NOT_APPLICABLE";
    public override int StatusCode => 409;

    public BackupNotApplicableException()
        : base("Backup is only for a feature harder than every active developer can take.")
    {
    }
}

public class DependentsResolutionRequiredException : AppException
{
    public override string ErrorCode => "DEPENDENTS_RESOLUTION_REQUIRED";
    public override int StatusCode => 409;

    public List<FeatureReferenceDto> AffectedDependents { get; }

    public override object Details => AffectedDependents;

    public DependentsResolutionRequiredException(List<FeatureReferenceDto> affectedDependents)
        : base("Other features depend on the feature being removed. Choose to remove them too, or restructure their dependencies.")
    {
        AffectedDependents = affectedDependents;
    }
}

public class InvalidReplacementException : AppException
{
    public override string ErrorCode => "INVALID_REPLACEMENT_FEATURE";
    public override int StatusCode => 409;

    public InvalidReplacementException()
        : base("The replacement must be the new feature or another feature that stays in the snapshot and does not depend on the removed one.")
    {
    }
}

