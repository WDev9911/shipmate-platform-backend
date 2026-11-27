using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;
using ShipMate.Application.Validators.Auth;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Tests.Application;

// The administrator named in AdminSeed:* is created once and never changed afterwards.
public class AdministratorSeederTests
{
    private const string Email = "admin@example.com";
    private const string Password = "Admin@12345";

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ILogger<AdministratorSeeder> _logger = Substitute.For<ILogger<AdministratorSeeder>>();
    private readonly Dictionary<string, string?> _settings = new();
    private readonly List<User> _added = [];

    public AdministratorSeederTests()
    {
        _hasher.Hash(Arg.Any<string>()).Returns(call => $"hash:{call.Arg<string>()}");
        _users.AddAsync(Arg.Do<User>(_added.Add));
    }

    private Task Seed()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(_settings).Build();
        return new AdministratorSeeder(_users, _hasher, configuration, _logger).SeedAsync();
    }

    private void Configure(string? email = Email, string? password = Password, string? displayName = null)
    {
        _settings["AdminSeed:Email"] = email;
        _settings["AdminSeed:Password"] = password;
        _settings["AdminSeed:DisplayName"] = displayName;
    }

    [Fact]
    public async Task NothingConfigured_DoesNothing_AndNeverTouchesTheDatabase()
    {
        await Seed();

        Assert.Empty(_added);
        await _users.DidNotReceive().GetByEmailAsync(Arg.Any<string>());
        await _users.DidNotReceive().SaveChangesAsync();
    }

    [Theory]
    [InlineData(null, Password)]
    [InlineData(Email, null)]
    [InlineData("", Password)]
    [InlineData(Email, "")]
    public async Task EmailOrPasswordMissing_DoesNothing(string? email, string? password)
    {
        Configure(email, password);

        await Seed();

        Assert.Empty(_added);
        await _users.DidNotReceive().GetByEmailAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task NoSuchAccount_CreatesAVerifiedActiveAdministratorWithAHashedPassword()
    {
        Configure(displayName: "Platform Admin");

        await Seed();

        var admin = Assert.Single(_added);
        Assert.Equal(Email, admin.Email);
        Assert.Equal("Platform Admin", admin.DisplayName);
        Assert.Equal(UserRole.Administrator, admin.Role);
        Assert.Equal(UserStatus.Active, admin.Status);
        Assert.True(admin.IsEmailVerified);
        Assert.NotNull(admin.EmailVerifiedAt);
        Assert.True(admin.HasPassword);
        Assert.Equal($"hash:{Password}", admin.PasswordHash);
        await _users.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task NoDisplayName_UsesTheDefault()
    {
        Configure(displayName: "  ");

        await Seed();

        Assert.Equal("Administrator", Assert.Single(_added).DisplayName);
    }

    [Fact]
    public async Task AnAdministratorAlreadyThere_IsLeftAlone_PasswordIncluded()
    {
        var existing = new User { Email = Email, Role = UserRole.Administrator, PasswordHash = "hash:Changed@1" };
        _users.GetByEmailAsync(Email).Returns(existing);
        Configure();

        await Seed();

        Assert.Empty(_added);
        Assert.Equal("hash:Changed@1", existing.PasswordHash);
        await _users.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task ADeveloperWithThatEmail_IsNeverPromoted()
    {
        var developer = new User { Email = Email, Role = UserRole.Developer };
        _users.GetByEmailAsync(Email).Returns(developer);
        Configure();

        await Seed();

        Assert.Equal(UserRole.Developer, developer.Role);
        Assert.Empty(_added);
        await _users.DidNotReceive().SaveChangesAsync();
    }

    [Theory]
    [InlineData("short1A")]
    [InlineData("nouppercase1")]
    [InlineData("NoDigitsHere")]
    public async Task AWeakPassword_CreatesNothing(string password)
    {
        Configure(password: password);

        await Seed();

        Assert.Empty(_added);
        await _users.DidNotReceive().SaveChangesAsync();
    }

    [Theory]
    [InlineData(Password, true)]
    [InlineData("Abcdefg1", true)]
    [InlineData("Abcdef1", false)]
    [InlineData("abcdefg1", false)]
    [InlineData("Abcdefgh", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ThePasswordRule_IsTheSameAsRegistrationAndReset(string? password, bool valid)
    {
        Assert.Equal(valid, PasswordRules.IsValid(password));
    }
}
