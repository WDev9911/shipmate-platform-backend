using NSubstitute;
using ShipMate.Application.DTOs.Admin;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;
using ShipMate.Domain.Policies;

namespace ShipMate.Tests.Application;

// The settings an administrator edits: reading, saving with a revision, the log of what changed, and trying patterns.
public class PlatformSettingsServiceTests
{
    private readonly IPlatformSettingsRepository _repository = Substitute.For<IPlatformSettingsRepository>();
    private readonly IAdminActionLogRepository _logs = Substitute.For<IAdminActionLogRepository>();
    private readonly PlatformSettingsService _service;
    private readonly PlatformSettings _settings = new()
    {
        ComplexityBudgetPerDeveloper = 10,
        DefaultDriftThreshold = 0.75m,
        MaintenanceCommitPatterns = ["fix typo", "refactor"],
        Revision = 4
    };
    private readonly List<AdminActionLog> _logged = [];
    private readonly Guid _admin = Guid.NewGuid();

    public PlatformSettingsServiceTests()
    {
        _repository.GetAsync().Returns(_settings);
        _logs.AddAsync(Arg.Do<AdminActionLog>(_logged.Add));
        _service = new PlatformSettingsService(_repository, _logs);
    }

    private UpdatePlatformSettingsRequest Same() => new()
    {
        ComplexityBudgetPerDeveloper = 10,
        DefaultDriftThreshold = 0.75m,
        MaintenanceCommitPatterns = ["fix typo", "refactor"],
        Revision = 4
    };

    // ---------- Reading ----------

    [Fact]
    public async Task Get_ReturnsTheValuesTheRevisionAndTheFixedLimits()
    {
        _settings.UpdatedAt = new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);
        _settings.UpdatedByUserId = _admin;

        var dto = await _service.GetAsync();

        Assert.Equal(10, dto.ComplexityBudgetPerDeveloper);
        Assert.Equal(0.75m, dto.DefaultDriftThreshold);
        Assert.Equal(["fix typo", "refactor"], dto.MaintenanceCommitPatterns);
        Assert.Equal(4, dto.Revision);
        Assert.Equal(_admin, dto.UpdatedByUserId);
        Assert.Equal(0.65m, dto.Limits.DriftThresholdHardFloor);
        Assert.Equal(0.70m, dto.Limits.DriftThresholdMin);
        Assert.Equal(0.85m, dto.Limits.DriftThresholdMax);
        Assert.Equal(3, dto.Limits.ManagerMaxThresholdChanges);
        Assert.Equal(7, dto.Limits.ManagerThresholdCooldownDays);
        Assert.Equal(5, dto.Limits.MinBudgetPerDeveloper);
        Assert.Equal(50, dto.Limits.MaxBudgetPerDeveloper);
    }

    [Fact]
    public async Task Get_HandsOutACopyOfThePatterns_NotTheListThatIsStored()
    {
        var dto = await _service.GetAsync();
        dto.MaintenanceCommitPatterns.Add("sneaky");

        Assert.Equal(2, _settings.MaintenanceCommitPatterns.Count);
    }

    // ---------- Saving ----------

    [Fact]
    public async Task Update_ChangesEveryFieldBumpsTheRevisionAndSaysWhoAndWhen()
    {
        var request = new UpdatePlatformSettingsRequest
        {
            ComplexityBudgetPerDeveloper = 15,
            DefaultDriftThreshold = 0.80m,
            MaintenanceCommitPatterns = ["chore", "docs"],
            Revision = 4
        };

        var dto = await _service.UpdateAsync(_admin, request);

        Assert.Equal(15, _settings.ComplexityBudgetPerDeveloper);
        Assert.Equal(0.80m, _settings.DefaultDriftThreshold);
        Assert.Equal(["chore", "docs"], _settings.MaintenanceCommitPatterns);
        Assert.Equal(5, _settings.Revision);
        Assert.Equal(_admin, _settings.UpdatedByUserId);
        Assert.NotNull(_settings.UpdatedAt);
        Assert.Equal(5, dto.Revision);
        await _repository.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Update_WritesOneLogLinePerFieldThatChanged_AndNoneForTheRest()
    {
        var request = Same();
        request.ComplexityBudgetPerDeveloper = 12;
        request.MaintenanceCommitPatterns = ["fix typo", "refactor", "chore"];

        await _service.UpdateAsync(_admin, request);

        Assert.Equal(2, _logged.Count);
        Assert.All(_logged, l =>
        {
            Assert.Equal(AdminAction.SettingsChanged, l.Action);
            Assert.Equal(_admin, l.AdminUserId);
            Assert.Null(l.TargetUserId);
        });
        var budget = _logged.Single(l => l.Subject == "complexity_budget_per_developer");
        Assert.Equal("10", budget.OldValue);
        Assert.Equal("12", budget.NewValue);
        var patterns = _logged.Single(l => l.Subject == "maintenance_commit_patterns");
        Assert.Equal("fix typo, refactor", patterns.OldValue);
        Assert.Equal("fix typo, refactor, chore", patterns.NewValue);
        Assert.DoesNotContain(_logged, l => l.Subject == "default_drift_threshold");
    }

    [Theory]
    [InlineData("0.8", "0.80")]
    [InlineData("0.80", "0.80")]
    [InlineData("0.7", "0.70")]
    public async Task Update_TheThresholdIsLoggedWithTwoDecimals_WhateverScaleItArrivedWith(string sent, string logged)
    {
        var request = Same();
        request.DefaultDriftThreshold = decimal.Parse(sent, System.Globalization.CultureInfo.InvariantCulture);

        await _service.UpdateAsync(_admin, request);

        var line = Assert.Single(_logged);
        Assert.Equal("0.75", line.OldValue);
        Assert.Equal(logged, line.NewValue);
    }

    [Fact]
    public async Task Update_TheThresholdIsLoggedWithADotWhateverTheCulture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("vi-VN");
        try
        {
            var request = Same();
            request.DefaultDriftThreshold = 0.80m;

            await _service.UpdateAsync(_admin, request);

            var line = Assert.Single(_logged);
            Assert.Equal("0.75", line.OldValue);
            Assert.Equal("0.80", line.NewValue);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact]
    public async Task Update_PatternsAreStoredTrimmedInLowerCaseAndWithoutRepeats()
    {
        var request = Same();
        request.MaintenanceCommitPatterns = ["  Chore  ", "DOCS", "docs "];

        // The validator refuses the repeat; the service is still safe if it is ever reached.
        await _service.UpdateAsync(_admin, request);

        Assert.Equal(["chore", "docs"], _settings.MaintenanceCommitPatterns);
    }

    [Fact]
    public async Task Update_TheSamePatternsInAnotherCaseOrWithSpaces_IsNoChange()
    {
        var request = Same();
        request.MaintenanceCommitPatterns = ["  FIX TYPO ", "Refactor"];

        await Assert.ThrowsAsync<NoChangeException>(() => _service.UpdateAsync(_admin, request));

        Assert.Equal(4, _settings.Revision);
    }

    [Fact]
    public async Task Update_NothingDiffers_IsNoChange_AndNothingIsWrittenOrSaved()
    {
        await Assert.ThrowsAsync<NoChangeException>(() => _service.UpdateAsync(_admin, Same()));

        Assert.Equal(4, _settings.Revision);
        Assert.Empty(_logged);
        await _repository.DidNotReceive().SaveChangesAsync();
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(0)]
    public async Task Update_BasedOnAnotherRevision_IsRefusedAndChangesNothing(int revision)
    {
        var request = Same();
        request.ComplexityBudgetPerDeveloper = 20;
        request.Revision = revision;

        var error = await Assert.ThrowsAsync<SettingsChangedByOtherException>(() => _service.UpdateAsync(_admin, request));

        Assert.Equal(409, error.StatusCode);
        Assert.Equal("SETTINGS_CHANGED_BY_OTHER", error.ErrorCode);
        Assert.Equal(10, _settings.ComplexityBudgetPerDeveloper);
        Assert.Equal(4, _settings.Revision);
        Assert.Empty(_logged);
        await _repository.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Update_TheRevisionIsCheckedBeforeNoChange()
    {
        var request = Same();
        request.Revision = 1;

        await Assert.ThrowsAsync<SettingsChangedByOtherException>(() => _service.UpdateAsync(_admin, request));
    }
}
