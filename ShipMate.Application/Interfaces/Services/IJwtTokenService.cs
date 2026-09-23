using ShipMate.Domain.Entities;

namespace ShipMate.Application.Interfaces.Services;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) GenerateAccessToken(User user);
}
