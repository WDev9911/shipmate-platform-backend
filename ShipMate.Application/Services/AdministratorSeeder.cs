using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Validators.Auth;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Application.Services;

/// <summary>
/// Creates the administrator account named in AdminSeed:* when the app starts and no such account exists yet.
/// An administrator is a separate account, never a developer promoted: registration always makes developers.
/// </summary>
public class AdministratorSeeder
{
    private const string DefaultDisplayName = "Administrator";

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdministratorSeeder> _logger;

    public AdministratorSeeder(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        ILogger<AdministratorSeeder> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        var email = _configuration["AdminSeed:Email"]?.Trim();
        var password = _configuration["AdminSeed:Password"];
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            _logger.LogInformation("No administrator is seeded: AdminSeed:Email and AdminSeed:Password are not both set.");
            return;
        }

        var existing = await _userRepository.GetByEmailAsync(email);
        if (existing is not null)
        {
            // Never reset the password of an administrator who is already there (they may have changed it since),
            // and never promote a developer: that is what a separate account is for.
            if (existing.Role != UserRole.Administrator)
            {
                _logger.LogError("AdminSeed:Email belongs to a developer account, which is never promoted. Use another email.");
            }

            return;
        }

        if (!PasswordRules.IsValid(password))
        {
            _logger.LogError("AdminSeed:Password is too weak (at least 8 characters, 1 uppercase letter and 1 digit); no administrator was created.");
            return;
        }

        var displayName = _configuration["AdminSeed:DisplayName"]?.Trim();
        await _userRepository.AddAsync(new User
        {
            Email = email,
            PasswordHash = _passwordHasher.Hash(password),
            HasPassword = true,
            DisplayName = string.IsNullOrEmpty(displayName) ? DefaultDisplayName : displayName,
            IsEmailVerified = true,
            EmailVerifiedAt = DateTime.UtcNow,
            Role = UserRole.Administrator,
            Status = UserStatus.Active
        });
        await _userRepository.SaveChangesAsync();

        _logger.LogInformation("Administrator account {Email} created.", email);
    }
}
