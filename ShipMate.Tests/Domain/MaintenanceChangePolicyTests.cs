using ShipMate.Domain.Policies;
using ShipMate.Domain.ValueObjects;

namespace ShipMate.Tests.Domain;

public class MaintenanceChangePolicyTests
{
    private static ChangedFile Code(string patch) => new("src/app.ts", "modified", patch);

    private static bool Skips(string message, params ChangedFile[] files) =>
        MaintenanceChangePolicy.IsMaintenanceOnly([message], files);

    [Fact]
    public void MaintenanceMessage_WithOnlyCommentChanges_IsMaintenance() =>
        Assert.True(Skips("refactor: tidy comments", Code("@@ -1,2 +1,2 @@\n-// old note\n+// new note")));

    [Fact]
    public void MaintenanceMessage_WithReindentedCode_IsMaintenance() =>
        Assert.True(Skips("format code", Code("@@ -1,2 +1,2 @@\n-function a(){return 1;}\n+function a() {\n+    return 1;\n+}")));

    [Fact]
    public void MaintenanceMessage_WithANewLineOfLogic_StillNeedsTheAi() =>
        Assert.False(Skips("refactor: cleanup", Code("@@ -1 +1,2 @@\n x = 1;\n+chargeCustomer(order);")));

    [Fact]
    public void MaintenanceMessage_WithAChangedToken_StillNeedsTheAi() =>
        Assert.False(Skips("fix typo", Code("@@ -1 +1 @@\n-const limit = 10;\n+const limit = 1000;")));

    [Fact]
    public void LogicMessage_NeverSkips_EvenForCommentOnlyChanges() =>
        Assert.False(Skips("add payment flow", Code("@@ -1 +1 @@\n-// a\n+// b")));

    [Fact]
    public void OnlyDocsAndConfigFiles_AreMaintenanceWithAMaintenanceMessage() =>
        Assert.True(Skips("bump version",
            new ChangedFile("README.md", "modified", "@@ -1 +1 @@\n-v1\n+v2"),
            new ChangedFile(".gitignore", "modified", "@@ -1 +1 @@\n+bin/"),
            new ChangedFile("LICENSE", "modified", null),
            new ChangedFile("docs/guide/setup.md", "added", "@@ -0,0 +1 @@\n+hello")));

    [Fact]
    public void ASourceFileWithoutAPatch_IsNeverSkipped() =>
        Assert.False(Skips("refactor", new ChangedFile("src/big.ts", "modified", null)));

    [Fact]
    public void OneLogicFileAmongDocs_NeedsTheAi() =>
        Assert.False(Skips("refactor",
            new ChangedFile("README.md", "modified", "@@ -1 +1 @@\n-a\n+b"),
            Code("@@ -1 +1 @@\n-run();\n+runDifferently();")));

    [Fact]
    public void EveryCommitMustLookLikeMaintenance() =>
        Assert.False(MaintenanceChangePolicy.IsMaintenanceOnly(
            ["refactor: tidy", "add export feature"], [Code("@@ -1 +1 @@\n-// a\n+// b")]));

    [Fact]
    public void NoCommits_NeverSkips() =>
        Assert.False(MaintenanceChangePolicy.IsMaintenanceOnly([], [Code("@@ -1 +1 @@\n-// a\n+// b")]));

    [Theory]
    [InlineData("#include <stdio.h>")]
    [InlineData("*ptr = 0;")]
    [InlineData("--counter;")]
    public void LinesThatLookLikeCommentMarksButAreCode_AreNotComments(string line) =>
        Assert.False(Skips("refactor", Code($"@@ -0,0 +1 @@\n+{line}")));

    [Theory]
    [InlineData("# a note")]
    [InlineData("/* block */")]
    [InlineData("<!-- note -->")]
    [InlineData("-- sql note")]
    public void RealCommentLines_AreIgnored(string line) =>
        Assert.True(Skips("refactor", Code($"@@ -0,0 +1 @@\n+{line}")));
}
