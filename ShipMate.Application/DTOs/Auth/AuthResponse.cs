using System.Text.Json.Serialization;

namespace ShipMate.Application.DTOs.Auth;

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAt { get; set; }

    // Never serialized to the client — delivered as an httpOnly cookie instead.
    // The controller reads this field directly (JsonIgnore only affects serialization) to set the cookie.
    [JsonIgnore]
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAt { get; set; }

    public UserDto User { get; set; } = null!;
}
