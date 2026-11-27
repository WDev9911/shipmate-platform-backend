using NSubstitute;
using ShipMate.Application.DTOs.Admin;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Services;
using ShipMate.Application.Validators.Admin;

namespace ShipMate.Tests.Application;

// The range of the AI usage report and the service around the report repository.
public class AdminReportsTests
{
    private static readonly DateTime Now = new(2026, 11, 24, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Range_Defaults_ToTheLast30Days()
    {
        var (from, to) = AdminReportsService.Range(new AiUsageQuery(), Now);

        Assert.Equal(Now, to);
        Assert.Equal(Now.AddDays(-30), from);
    }

    [Fact]
    public void Range_OnlyTo_Starts30DaysBeforeIt()
    {
        var to = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        var (from, _) = AdminReportsService.Range(new AiUsageQuery { To = to }, Now);

        Assert.Equal(to.AddDays(-30), from);
    }

    [Fact]
    public void Range_ADateWithoutAZone_IsReadAsUtc_NotAsTheServersLocalTime()
    {
        var (from, to) = AdminReportsService.Range(new AiUsageQuery
        {
            From = new DateTime(2026, 11, 1), To = new DateTime(2026, 11, 2)
        }, Now);

        Assert.Equal(DateTimeKind.Utc, from.Kind);
        Assert.Equal(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), from);
        Assert.Equal(DateTimeKind.Utc, to.Kind);
    }

    [Fact]
    public void Range_ALocalTime_IsConvertedToUtc()
    {
        var local = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Local);

        var (from, _) = AdminReportsService.Range(new AiUsageQuery { From = local, To = Now }, Now);

        Assert.Equal(local.ToUniversalTime(), from);
    }

    [Fact]
    public async Task Usage_AsksTheRepositoryForTheRange_AndEchoesIt()
    {
        var reports = Substitute.For<IPlatformReportRepository>();
        var from = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc);
        reports.GetAiUsageAsync(from, to).Returns([new AiUsageRowDto { Calls = 3 }]);

        var usage = await new AdminReportsService(reports).GetAiUsageAsync(new AiUsageQuery { From = from, To = to });

        Assert.Equal(from, usage.From);
        Assert.Equal(to, usage.To);
        Assert.Equal(3, Assert.Single(usage.Rows).Calls);
    }

    // ---------- validation ----------

    private readonly AiUsageQueryValidator _validator = new();

    [Fact]
    public void Validator_NoDates_IsValid() => Assert.True(_validator.Validate(new AiUsageQuery()).IsValid);

    [Fact]
    public void Validator_FromAfterTo_IsInvalid() =>
        Assert.False(_validator.Validate(new AiUsageQuery { From = Now, To = Now.AddDays(-1) }).IsValid);

    [Fact]
    public void Validator_FromEqualToTo_IsInvalid() =>
        Assert.False(_validator.Validate(new AiUsageQuery { From = Now, To = Now }).IsValid);

    [Theory]
    [InlineData(365, true)]
    [InlineData(366, false)]
    public void Validator_TheRange_IsAtMostAYear(int days, bool valid) =>
        Assert.Equal(valid, _validator.Validate(new AiUsageQuery { From = Now.AddDays(-days), To = Now }).IsValid);

    [Fact]
    public void Validator_OnlyAFromInTheFuture_IsInvalid() =>
        Assert.False(_validator.Validate(new AiUsageQuery { From = DateTime.UtcNow.AddDays(1) }).IsValid);
}
