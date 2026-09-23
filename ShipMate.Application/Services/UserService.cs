using AutoMapper;
using ShipMate.Application.DTOs.Auth;
using ShipMate.Application.DTOs.Users;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IMapper _mapper;

    public UserService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        IMapper mapper)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _mapper = mapper;
    }

    public async Task<UserDto> GetProfileAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException("User", userId);

        return _mapper.Map<UserDto>(user);
    }

    public async Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException("User", userId);

        if (request.DisplayName is not null)
        {
            user.DisplayName = request.DisplayName;
        }

        if (request.NotificationPreference is not null)
        {
            user.NotificationPreference = request.NotificationPreference.Value;
        }

        user.UpdatedAt = DateTime.UtcNow;
        _userRepository.Update(user);

        await _userRepository.SaveChangesAsync();

        return _mapper.Map<UserDto>(user);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException("User", userId);

        if (!user.HasPassword || user.PasswordHash is null)
        {
            throw new NoPasswordSetException();
        }

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new IncorrectPasswordException();
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        _userRepository.Update(user);

        // Same policy as ResetPasswordAsync: changing the password is security-sensitive,
        // force re-login on every device (including this one).
        var activeTokens = await _refreshTokenRepository.GetActiveTokensByUserIdAsync(userId);
        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
        }

        await _userRepository.SaveChangesAsync();
    }
}
