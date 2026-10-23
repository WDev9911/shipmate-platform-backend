using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;
using ShipMate.Domain.Policies;

namespace ShipMate.Tests.Domain;

public class FeatureChallengePolicyTests
{
    private static SnapshotFeature F(
        string name, int complexity, FeatureOrigin origin = FeatureOrigin.FromAi,
        SnapshotFeatureStatus status = SnapshotFeatureStatus.Locked) => new()
    {
        FeatureId = Guid.NewGuid(),
        Name = name,
        Complexity = complexity,
        Origin = origin,
        Status = status
    };

    private static MvpSnapshot Snapshot(params SnapshotFeature[] features)
    {
        var snapshot = new MvpSnapshot { Version = 1, WorkspaceId = Guid.NewGuid(), DevCountAtLock = 1 };
        foreach (var feature in features)
        {
            snapshot.Features.Add(feature);
        }

        return snapshot;
    }

    private static void Link(MvpSnapshot snapshot, SnapshotFeature feature, SnapshotFeature prerequisite) =>
        snapshot.Dependencies.Add(new SnapshotDependency { FeatureId = feature.FeatureId, DependsOnFeatureId = prerequisite.FeatureId });

    private static FeatureChallenge Challenge(int complexity = 3) =>
        new() { Name = "new feature", Description = "d", Complexity = complexity };

    private static ChallengeRecommendation Recommend(MvpSnapshot snapshot, int complexity, params int?[] skills) =>
        ChallengeRecommendationPolicy.Recommend(MvpSnapshotEvolution.ActiveFeatures(snapshot), complexity, skills);

    // ---- recommendation ----

    [Fact]
    public void HarderThanEveryDeveloper_RecommendsBackup()
    {
        var recommendation = Recommend(Snapshot(F("a", 2)), 5, 3, 4);

        Assert.Equal(ChallengeAction.Backup, recommendation.Action);
    }

    [Fact]
    public void FitsTheBudget_RecommendsAddingDirectly()
    {
        var recommendation = Recommend(Snapshot(F("a", 3), F("b", 3)), 4, 5);

        Assert.Equal(ChallengeAction.AddDirectly, recommendation.Action);
    }

    [Fact]
    public void BudgetFullAndSkillEnough_RecommendsReplacingTheLeastComplexNonCommittedFeatureThatMakesRoom()
    {
        var small = F("small", 1);
        var medium = F("medium", 4);
        var big = F("big", 5);
        var snapshot = Snapshot(small, medium, big);
        var recommendation = Recommend(snapshot, 3, 5);

        Assert.Equal(ChallengeAction.RemoveAndReplace, recommendation.Action);
        Assert.Equal(medium.FeatureId, recommendation.FeatureToRemove!.Id);
    }

    [Fact]
    public void PrefersNonCommittedEvenWhenACommittedFeatureIsLessComplex()
    {
        var committed = F("committed", 4, FeatureOrigin.FromCommittedList);
        var fromAi = F("from-ai", 5);
        var recommendation = Recommend(Snapshot(committed, fromAi, F("rest", 1)), 4, 5);

        Assert.Equal(fromAi.FeatureId, recommendation.FeatureToRemove!.Id);
    }

    [Fact]
    public void OnlyACommittedFeatureMakesRoom_StillSuggestsItSoTheManagerCanConfirm()
    {
        var committed = F("committed", 5, FeatureOrigin.FromCommittedList);
        var recommendation = Recommend(Snapshot(committed, F("a", 2), F("b", 3)), 5, 5);

        Assert.Equal(committed.FeatureId, recommendation.FeatureToRemove!.Id);
        Assert.True(recommendation.FeatureToRemove.IsCommitted);
    }

    [Fact]
    public void NoSingleRemovalMakesRoom_RecommendsNothing()
    {
        var recommendation = Recommend(Snapshot(F("a", 1), F("b", 1), F("c", 4), F("d", 4)), 5, 5);

        Assert.Null(recommendation.Action);
    }

    [Fact]
    public void BackupFeaturesCountTowardTheBudget_ReplacedOnesDoNot()
    {
        var snapshot = Snapshot(
            F("a", 5), F("b", 3),
            F("backup", 2, status: SnapshotFeatureStatus.Backup),
            F("gone", 5, status: SnapshotFeatureStatus.Replaced));

        // 5 + 3 + 2 = 10, so even a complexity-1 feature does not fit.
        Assert.NotEqual(ChallengeAction.AddDirectly, Recommend(snapshot, 1, 5).Action);
    }

    // ---- next version ----

    [Fact]
    public void NextVersion_IsAFullCopyWithTheNextNumberAndLeavesThePreviousAlone()
    {
        var a = F("a", 2);
        var b = F("b", 3);
        var previous = Snapshot(a, b);
        Link(previous, b, a);

        var next = MvpSnapshotEvolution.NextVersion(previous, Guid.NewGuid(), devCount: 2);

        Assert.Equal(2, next.Version);
        Assert.Equal(2, next.DevCountAtLock);
        Assert.Equal(2, next.Features.Count);
        Assert.Single(next.Dependencies);
        Assert.NotSame(previous.Features.First(), next.Features.First());

        next.Features.First().Complexity = 5;
        Assert.Equal(2, previous.Features.First().Complexity);
    }

    [Fact]
    public void AddFeature_AlwaysComesFromAi()
    {
        var next = Snapshot();
        var challenge = Challenge(4);

        MvpSnapshotEvolution.AddFeature(next, challenge, SnapshotFeatureStatus.Backup);

        var added = Assert.Single(next.Features);
        Assert.Equal(FeatureOrigin.FromAi, added.Origin);
        Assert.Equal(SnapshotFeatureStatus.Backup, added.Status);
        Assert.Equal(challenge.FeatureId, added.FeatureId);
        Assert.Equal(4, added.Complexity);
    }

    // ---- dependents ----

    [Fact]
    public void DependentsOf_FollowsDependenciesThroughEveryLevelOnce()
    {
        var a = F("a", 1);
        var b = F("b", 1);
        var c = F("c", 1);
        var d = F("d", 1);
        var snapshot = Snapshot(a, b, c, d);
        Link(snapshot, b, a);
        Link(snapshot, c, b);
        Link(snapshot, c, a);

        var dependents = MvpSnapshotEvolution.DependentsOf(snapshot, a.FeatureId);

        Assert.Equal(2, dependents.Count);
        Assert.Contains(b.FeatureId, dependents);
        Assert.Contains(c.FeatureId, dependents);
        Assert.DoesNotContain(d.FeatureId, dependents);
    }

    // ---- remove and replace ----

    [Fact]
    public void RemoveAndReplace_NoDependents_SwapsTheFeatures()
    {
        var a = F("a", 2);
        var b = F("b", 3);
        var next = Snapshot(a, b);
        var challenge = Challenge();

        var error = MvpSnapshotEvolution.RemoveAndReplace(next, challenge, a.FeatureId, null, null);

        Assert.Equal(EvolutionError.None, error);
        Assert.Equal(SnapshotFeatureStatus.Replaced, a.Status);
        Assert.Equal(SnapshotFeatureStatus.Locked, b.Status);
        Assert.Contains(next.Features, f => f.FeatureId == challenge.FeatureId && f.Status == SnapshotFeatureStatus.Locked);
    }

    [Fact]
    public void RemoveAndReplace_UnknownOrAlreadyReplacedFeature_IsNotFound()
    {
        var gone = F("gone", 2, status: SnapshotFeatureStatus.Replaced);
        var next = Snapshot(gone);

        Assert.Equal(EvolutionError.RemovedFeatureNotFound,
            MvpSnapshotEvolution.RemoveAndReplace(next, Challenge(), gone.FeatureId, null, null));
        Assert.Equal(EvolutionError.RemovedFeatureNotFound,
            MvpSnapshotEvolution.RemoveAndReplace(next, Challenge(), Guid.NewGuid(), null, null));
    }

    [Fact]
    public void RemoveAndReplace_WithDependentsAndNoChoice_AsksForOneAndChangesNothing()
    {
        var a = F("a", 2);
        var b = F("b", 3);
        var next = Snapshot(a, b);
        Link(next, b, a);

        var error = MvpSnapshotEvolution.RemoveAndReplace(next, Challenge(), a.FeatureId, null, null);

        Assert.Equal(EvolutionError.DependentsResolutionRequired, error);
        Assert.Equal(SnapshotFeatureStatus.Locked, a.Status);
        Assert.Equal(2, next.Features.Count);
    }

    [Fact]
    public void RemoveAndReplace_RemoveDependents_RemovesEveryLevelAndTheirLinks()
    {
        var a = F("a", 1);
        var b = F("b", 1);
        var c = F("c", 1);
        var keep = F("keep", 1);
        var next = Snapshot(a, b, c, keep);
        Link(next, b, a);
        Link(next, c, b);
        Link(next, b, a);
        Link(next, c, b);

        var error = MvpSnapshotEvolution.RemoveAndReplace(
            next, Challenge(), a.FeatureId, RemovalDependentsResolution.RemoveDependents, null);

        Assert.Equal(EvolutionError.None, error);
        Assert.All([a, b, c], f => Assert.Equal(SnapshotFeatureStatus.Replaced, f.Status));
        Assert.Equal(SnapshotFeatureStatus.Locked, keep.Status);
        Assert.Empty(next.Dependencies);
    }

    [Fact]
    public void RemoveAndReplace_Restructure_DirectDependentsNowNeedTheNewFeature()
    {
        var a = F("a", 1);
        var b = F("b", 1);
        var c = F("c", 1);
        var next = Snapshot(a, b, c);
        Link(next, b, a);
        Link(next, c, b);
        var challenge = Challenge();

        var error = MvpSnapshotEvolution.RemoveAndReplace(
            next, challenge, a.FeatureId, RemovalDependentsResolution.Restructure, null);

        Assert.Equal(EvolutionError.None, error);
        Assert.Equal(SnapshotFeatureStatus.Locked, b.Status);
        Assert.Equal(SnapshotFeatureStatus.Locked, c.Status);
        Assert.Contains(next.Dependencies, d => d.FeatureId == b.FeatureId && d.DependsOnFeatureId == challenge.FeatureId);
        Assert.Contains(next.Dependencies, d => d.FeatureId == c.FeatureId && d.DependsOnFeatureId == b.FeatureId);
        Assert.DoesNotContain(next.Dependencies, d => d.DependsOnFeatureId == a.FeatureId);
    }

    [Fact]
    public void RemoveAndReplace_Restructure_CanPointAtAnotherExistingFeature()
    {
        var a = F("a", 1);
        var b = F("b", 1);
        var other = F("other", 1);
        var next = Snapshot(a, b, other);
        Link(next, b, a);

        var error = MvpSnapshotEvolution.RemoveAndReplace(
            next, Challenge(), a.FeatureId, RemovalDependentsResolution.Restructure, other.FeatureId);

        Assert.Equal(EvolutionError.None, error);
        Assert.Contains(next.Dependencies, d => d.FeatureId == b.FeatureId && d.DependsOnFeatureId == other.FeatureId);
    }

    [Fact]
    public void RemoveAndReplace_Restructure_RejectsReplacementsThatCannotWork()
    {
        var a = F("a", 1);
        var b = F("b", 1);
        var c = F("c", 1);
        var gone = F("gone", 1, status: SnapshotFeatureStatus.Replaced);

        foreach (var invalid in new[] { a.FeatureId, b.FeatureId, c.FeatureId, gone.FeatureId, Guid.NewGuid() })
        {
            var next = Snapshot(a, b, c, gone);
            Link(next, b, a);
            Link(next, c, b);

            var error = MvpSnapshotEvolution.RemoveAndReplace(
                next, Challenge(), a.FeatureId, RemovalDependentsResolution.Restructure, invalid);

            Assert.Equal(EvolutionError.ReplacementInvalid, error);
        }
    }
}
