namespace ShipMate.Application.DTOs.Auth;

public class ResetPasswordRequest
{
    public string ResetTicket { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
