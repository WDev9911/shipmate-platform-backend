using ShipMate.Domain.Entities;
using ShipMate.Domain.Policies;

namespace ShipMate.Tests.Domain;

public class ProductionUrlPolicyTests
{
    [Theory]
    [InlineData(null, ProductionUrlError.Empty)]
    [InlineData("   ", ProductionUrlError.Empty)]
    [InlineData("shipmate.app", ProductionUrlError.NotAbsolute)]
    [InlineData("http://shipmate.app", ProductionUrlError.NotHttps)]
    [InlineData("ftp://shipmate.app", ProductionUrlError.NotHttps)]
    [InlineData("file:///etc/passwd", ProductionUrlError.NotHttps)]
    [InlineData("https://shipmate.app:8443", ProductionUrlError.NotDefaultPort)]
    [InlineData("https://shipmate.app:22", ProductionUrlError.NotDefaultPort)]
    [InlineData("https://admin:secret@shipmate.app", ProductionUrlError.HasCredentials)]
    [InlineData("https://localhost", ProductionUrlError.LocalHost)]
    [InlineData("https://LOCALHOST./", ProductionUrlError.LocalHost)]
    [InlineData("https://api.localhost", ProductionUrlError.LocalHost)]
    public void Refuses_WhatIsNotAPublicHttpsAddress(string? input, ProductionUrlError expected)
    {
        Assert.Equal(expected, ProductionUrlPolicy.TryNormalize(input, out var url));
        Assert.Null(url);
    }

    [Fact]
    public void Refuses_ATooLongAddress()
    {
        var input = "https://shipmate.app/" + new string('a', ProductionUrlPolicy.MaxLength);

        Assert.Equal(ProductionUrlError.TooLong, ProductionUrlPolicy.TryNormalize(input, out _));
    }

    [Theory]
    [InlineData("https://ShipMate.App", "https://shipmate.app/")]
    [InlineData("  https://shipmate.app/booking?ref=card#top ", "https://shipmate.app/booking?ref=card")]
    [InlineData("https://shipmate.app:443/", "https://shipmate.app/")]
    [InlineData("https://shipmate.app/files/a%2Fb%20c", "https://shipmate.app/files/a%2Fb%20c")]
    public void Accepts_AndNormalizes(string input, string expected)
    {
        Assert.Equal(ProductionUrlError.None, ProductionUrlPolicy.TryNormalize(input, out var url));
        Assert.Equal(expected, url!.AbsoluteUri);
    }
}

public class ShipOwnershipTests
{
    private const string Token = "shipmate-0123456789abcdef0123456789abcdef";

    [Fact]
    public void NewToken_IsRandom_AndRecognisable()
    {
        var first = ShipOwnership.NewToken();
        var second = ShipOwnership.NewToken();

        Assert.Matches("^shipmate-[0-9a-f]{32}$", first);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void EveryWorkspace_IsBornWithItsOwnToken()
    {
        Assert.NotEqual(new Workspace().ShipOwnershipToken, new Workspace().ShipOwnershipToken);
        Assert.StartsWith("shipmate-", new Workspace().ShipOwnershipToken);
    }

    [Theory]
    [InlineData(Token)]
    [InlineData(Token + "\n")]
    [InlineData("  " + Token + "\r\n")]
    [InlineData("\uFEFF" + Token)]
    [InlineData("shipmate-ffffffffffffffffffffffffffffffff\n" + Token + "\n")]
    public void TheFileProvesOwnership_WhenALineIsTheToken(string content)
    {
        Assert.True(ShipOwnership.FileProves(content, Token));
    }

    [Theory]
    [InlineData("")]
    [InlineData("shipmate-ffffffffffffffffffffffffffffffff")]
    [InlineData("token: " + Token)]
    [InlineData("SHIPMATE-0123456789ABCDEF0123456789ABCDEF")]
    public void TheFileProvesNothing_WithoutTheExactToken(string content)
    {
        Assert.False(ShipOwnership.FileProves(content, Token));
    }

    [Fact]
    public void AnEmptyToken_IsNeverProven()
    {
        Assert.False(ShipOwnership.FileProves("", ""));
        Assert.False(ShipOwnership.FileProves("\n", " "));
    }
}

public class ShipAttemptLimitPolicyTests
{
    private static readonly DateTime Now = new(2026, 12, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void BelowTheLimit_MayRunNow()
    {
        var times = Enumerable.Range(1, ShipAttemptLimitPolicy.MaxAttemptsPerWindow - 1).Select(i => Now.AddMinutes(-i)).ToList();

        Assert.Null(ShipAttemptLimitPolicy.RetryAfter(times, Now));
    }

    [Fact]
    public void AtTheLimit_WaitsUntilTheOldestLeavesTheWindow()
    {
        var times = Enumerable.Range(1, ShipAttemptLimitPolicy.MaxAttemptsPerWindow).Select(i => Now.AddMinutes(-i * 5)).ToList();

        var retryAfter = ShipAttemptLimitPolicy.RetryAfter(times, Now);

        Assert.Equal(Now.AddMinutes(-50) + ShipAttemptLimitPolicy.Window, retryAfter);
    }

    [Fact]
    public void AttemptsOlderThanTheWindow_DoNotCount()
    {
        var times = Enumerable.Range(0, 30).Select(i => Now.AddHours(-2).AddMinutes(-i)).ToList();

        Assert.Null(ShipAttemptLimitPolicy.RetryAfter(times, Now));
    }

    [Fact]
    public void WellOverTheLimit_WaitsForEnoughToLeave()
    {
        // 12 attempts one minute apart: one more may run once three of them left.
        var times = Enumerable.Range(1, 12).Select(i => Now.AddMinutes(-i)).ToList();

        var retryAfter = ShipAttemptLimitPolicy.RetryAfter(times, Now);

        Assert.Equal(Now.AddMinutes(-10) + ShipAttemptLimitPolicy.Window, retryAfter);
    }
}
