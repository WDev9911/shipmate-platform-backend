namespace ShipMate.Application.Exceptions;

public class ShipWorkspaceArchivedException : AppException
{
    public override string ErrorCode => "SHIP_WORKSPACE_ARCHIVED";
    public override int StatusCode => 409;

    public ShipWorkspaceArchivedException()
        : base("This workspace is archived. Archived products can't be shipped.")
    {
    }
}

public class ShipNotLockedException : AppException
{
    public override string ErrorCode => "SHIP_NOT_LOCKED";
    public override int StatusCode => 409;

    public ShipNotLockedException()
        : base("There is nothing to ship yet: the MVP scope hasn't been locked.")
    {
    }
}

public class ShipVersionNotLatestException : AppException
{
    private readonly int _latestVersion;

    public override string ErrorCode => "SHIP_VERSION_NOT_LATEST";
    public override int StatusCode => 409;
    public override object Details => new { LatestVersion = _latestVersion };

    public ShipVersionNotLatestException(int latestVersion)
        : base($"Only the newest version of the scope can be shipped, and that is version {latestVersion}.")
    {
        _latestVersion = latestVersion;
    }
}

public class ShipAlreadyShippedException : AppException
{
    public override string ErrorCode => "SHIP_ALREADY_SHIPPED";
    public override int StatusCode => 409;

    public ShipAlreadyShippedException()
        : base("This version already has its Ship Card. A new version, from an approved Feature Challenge, can be shipped again.")
    {
    }
}

public class ShipGovernanceSuspendedException : AppException
{
    public override string ErrorCode => "SHIP_GOVERNANCE_SUSPENDED";
    public override int StatusCode => 409;

    public ShipGovernanceSuspendedException()
        : base("ShipMate lost access to the repository, so it can't vouch for this release. Install the ShipMate GitHub App again, then submit code once.")
    {
    }
}

public record ShipFeatureRef(Guid FeatureId, string Name);

public class ShipFeaturesNotApprovedException : AppException
{
    private readonly IReadOnlyList<ShipFeatureRef> _features;

    public override string ErrorCode => "SHIP_FEATURES_NOT_APPROVED";
    public override int StatusCode => 409;
    public override object Details => new { Features = _features };

    public ShipFeaturesNotApprovedException(IReadOnlyList<ShipFeatureRef> features)
        : base("Every locked feature of this version must have its latest code approved in BUILD before it ships.")
    {
        _features = features;
    }
}

public class ShipVerifyIncompleteException : AppException
{
    private readonly IReadOnlyList<string> _missingItems;

    public override string ErrorCode => "SHIP_VERIFY_INCOMPLETE";
    public override int StatusCode => 409;
    public override object Details => new { MissingItems = _missingItems };

    public ShipVerifyIncompleteException(IReadOnlyList<string> missingItems)
        : base("The Verification Checklist of this version isn't finished.")
    {
        _missingItems = missingItems;
    }
}

public class ShipAuditChainBrokenException : AppException
{
    private readonly long _brokenAtSequence;

    public override string ErrorCode => "SHIP_AUDIT_CHAIN_BROKEN";
    public override int StatusCode => 409;
    public override object Details => new { BrokenAtSequence = _brokenAtSequence };

    public ShipAuditChainBrokenException(long brokenAtSequence)
        : base("The workspace's audit history was altered, so a Ship Card can't vouch for it.")
    {
        _brokenAtSequence = brokenAtSequence;
    }
}

public class ShipUrlInvalidException : AppException
{
    private readonly string _reason;

    public override string ErrorCode => "SHIP_URL_INVALID";
    public override int StatusCode => 400;
    public override object Details => new { Reason = _reason };

    public ShipUrlInvalidException(string reason)
        : base("Enter the public https address of the product, without a port or a user name.")
    {
        _reason = reason;
    }
}

public class ShipRateLimitedException : AppException
{
    private readonly DateTime _retryAfter;

    public override string ErrorCode => "SHIP_RATE_LIMITED";
    public override int StatusCode => 429;
    public override object Details => new { RetryAfter = _retryAfter };

    public ShipRateLimitedException(DateTime retryAfter)
        : base("Too many ship attempts in the last hour. Fix the deployment and try again later.")
    {
        _retryAfter = retryAfter;
    }
}
