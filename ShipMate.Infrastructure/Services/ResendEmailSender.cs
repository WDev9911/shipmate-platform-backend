using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.Infrastructure.Services;

public class ResendEmailSender : IEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly string _fromEmail;
    private readonly ILogger<ResendEmailSender> _logger;

    public ResendEmailSender(HttpClient httpClient, IConfiguration configuration, ILogger<ResendEmailSender> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _fromEmail = configuration["Resend:FromEmail"] ?? "onboarding@resend.dev";
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        var payload = new
        {
            from = _fromEmail,
            to = new[] { toEmail },
            subject,
            html = htmlBody
        };

        var response = await _httpClient.PostAsJsonAsync("emails", payload);
        if (!response.IsSuccessStatusCode)
        {
            // The reason (an address Resend refuses, a sender that is not verified) is only in the body.
            _logger.LogWarning("Resend refused the email with {Status}: {Body}", (int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> TrySendAsync(string toEmail, string subject, string htmlBody)
    {
        try
        {
            await SendAsync(toEmail, subject, htmlBody);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notice email \"{Subject}\" was not sent", subject);
            return false;
        }
    }
}
