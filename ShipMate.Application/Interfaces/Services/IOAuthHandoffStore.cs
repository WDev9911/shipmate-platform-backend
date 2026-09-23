using ShipMate.Application.DTOs.Auth;

namespace ShipMate.Application.Interfaces.Services;

/// <summary>
/// Short-lived, single-use handoff between the GitHub callback (a browser redirect landing on the
/// backend) and the FE: the callback stores the freshly issued AuthResponse here and hands the FE
/// only an opaque code, avoiding ever putting real tokens in a URL.
/// </summary>
public interface IOAuthHandoffStore
{
    string Create(AuthResponse response);
    AuthResponse? Consume(string handoffCode);
}
