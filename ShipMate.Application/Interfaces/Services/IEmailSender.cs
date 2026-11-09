namespace ShipMate.Application.Interfaces.Services;

public interface IEmailSender
{
    // For mail the user needs to continue (verification code, password reset): a failure must surface.
    Task SendAsync(string toEmail, string subject, string htmlBody);

    // For notices only: a failure is logged and returns false, so it never breaks the action that triggered it.
    Task<bool> TrySendAsync(string toEmail, string subject, string htmlBody);
}
