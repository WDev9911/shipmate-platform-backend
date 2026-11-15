using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;
using ShipMate.Domain.Policies;

namespace ShipMate.Tests.Domain;

public class FeatureBuildStatePolicyTests
{
    private static BuildSubmission InScope(BuildSubmissionStatus status = BuildSubmissionStatus.InScope) =>
        new() { Status = status, ConfidenceScore = 0.90m, ThresholdAtCheck = 0.75m };

    private static BuildSubmission BeyondScope(BuildSubmissionStatus status = BuildSubmissionStatus.Drift) =>
        new() { Status = status, ConfidenceScore = 0.40m, ThresholdAtCheck = 0.75m };

    private static FeatureBuildState Evaluate(
        BuildSubmission? newest, bool hasOpenAlert = false, DriftAlertStatus? latestAlert = null) =>
        FeatureBuildStatePolicy.Evaluate(newest, hasOpenAlert, latestAlert);

    [Fact]
    public void NoSubmission_IsNotSubmitted() =>
        Assert.Equal(FeatureBuildState.NotSubmitted, Evaluate(null));

    [Fact]
    public void NewestApproved_IsApproved_EvenWithAnOldResolvedAlert() =>
        Assert.Equal(FeatureBuildState.Approved,
            Evaluate(InScope(BuildSubmissionStatus.Approved), latestAlert: DriftAlertStatus.Resolved));

    [Theory]
    [InlineData(BuildSubmissionStatus.Analyzing, FeatureBuildState.Analyzing)]
    [InlineData(BuildSubmissionStatus.Superseded, FeatureBuildState.Analyzing)]
    [InlineData(BuildSubmissionStatus.Failed, FeatureBuildState.Failed)]
    public void NewestWithoutVerdict_FollowsItsStatus(BuildSubmissionStatus status, FeatureBuildState expected) =>
        Assert.Equal(expected, Evaluate(new BuildSubmission { Status = status }));

    [Fact]
    public void InScope_NoAlert_IsReadyToApprove() =>
        Assert.Equal(FeatureBuildState.ReadyToApprove, Evaluate(InScope()));

    [Theory]
    [InlineData(DriftAlertStatus.Unresolved)]
    [InlineData(DriftAlertStatus.Acknowledged)]
    public void Drift_WithAnOpenAlert_IsDriftUnresolved(DriftAlertStatus alert) =>
        Assert.Equal(FeatureBuildState.DriftUnresolved, Evaluate(BeyondScope(), hasOpenAlert: true, latestAlert: alert));

    [Fact]
    public void Drift_Overridden_IsReadyToApprove() =>
        Assert.Equal(FeatureBuildState.ReadyToApprove, Evaluate(BeyondScope(), latestAlert: DriftAlertStatus.Overridden));

    [Fact]
    public void Drift_FindingsAboveTheThreshold_StillNeedsAnOverride()
    {
        var submission = InScope(BuildSubmissionStatus.Drift);
        submission.FindingsJson = """[{"file":"a.cs","reason":"outside"}]""";

        Assert.Equal(FeatureBuildState.DriftUnresolved, Evaluate(submission, latestAlert: DriftAlertStatus.Resolved));
    }

    [Theory]
    [InlineData(DriftAlertStatus.Overridden, FeatureBuildState.ReadyToApprove)]
    [InlineData(DriftAlertStatus.Resolved, FeatureBuildState.DriftUnresolved)]
    public void SkippedMaintenance_RepeatingADriftVerdict_DependsOnTheOverride(
        DriftAlertStatus latestAlert, FeatureBuildState expected) =>
        Assert.Equal(expected, Evaluate(BeyondScope(BuildSubmissionStatus.SkippedMaintenance), latestAlert: latestAlert));

    [Fact]
    public void SkippedMaintenance_RepeatingAnInScopeVerdict_IsReadyToApprove() =>
        Assert.Equal(FeatureBuildState.ReadyToApprove, Evaluate(InScope(BuildSubmissionStatus.SkippedMaintenance)));
}
