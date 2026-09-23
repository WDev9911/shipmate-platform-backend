using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.Infrastructure.Services;

public class ResendEmailSender : IEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly string _fromEmail;

    public ResendEmailSender(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
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
        response.EnsureSuccessStatusCode();
    }
}
