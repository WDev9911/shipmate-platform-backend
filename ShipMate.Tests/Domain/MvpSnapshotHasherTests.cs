using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;
using ShipMate.Domain.Policies;

namespace ShipMate.Tests.Domain;

public class MvpSnapshotHasherTests
{
    private static MvpSnapshot Snapshot(Guid first, Guid second) => new()
    {
        Version = 1,
        DevCountAtLock = 2,
        Features =
        {
            new SnapshotFeature { FeatureId = first, Name = "a", Description = "d", Complexity = 2, Origin = FeatureOrigin.FromAi },
            new SnapshotFeature { FeatureId = second, Name = "b", Description = "d", Complexity = 3, Origin = FeatureOrigin.FromAi }
        },
        Dependencies = { new SnapshotDependency { FeatureId = second, DependsOnFeatureId = first } }
    };

    [Fact]
    public void SameContent_GivesSameHash()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        Assert.Equal(
            MvpSnapshotHasher.Compute(Snapshot(first, second)),
            MvpSnapshotHasher.Compute(Snapshot(first, second)));
    }

    [Fact]
    public void Hash_IsA64CharacterHexString()
    {
        var hash = MvpSnapshotHasher.Compute(Snapshot(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void FeatureOrderDoesNotMatter()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var reversed = Snapshot(first, second);
        var features = reversed.Features.Reverse().ToList();
        reversed.Features.Clear();
        foreach (var feature in features)
        {
            reversed.Features.Add(feature);
        }

        Assert.Equal(
            MvpSnapshotHasher.Compute(Snapshot(first, second)),
            MvpSnapshotHasher.Compute(reversed));
    }

    [Fact]
    public void ChangingAnyFieldChangesTheHash()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var original = MvpSnapshotHasher.Compute(Snapshot(first, second));

        var complexity = Snapshot(first, second);
        complexity.Features.First().Complexity++;

        var devCount = Snapshot(first, second);
        devCount.DevCountAtLock = 3;

        var dependency = Snapshot(first, second);
        dependency.Dependencies.Clear();

        Assert.NotEqual(original, MvpSnapshotHasher.Compute(complexity));
        Assert.NotEqual(original, MvpSnapshotHasher.Compute(devCount));
        Assert.NotEqual(original, MvpSnapshotHasher.Compute(dependency));
    }
}
