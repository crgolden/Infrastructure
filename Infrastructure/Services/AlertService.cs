namespace Infrastructure.Services;

using Infrastructure.Models;
using Microsoft.Extensions.Options;
using Resend;

public sealed class AlertService(IResend resend, IOptions<AlertOptions> options) : IAlertService
{
    private const string From = "noreply@crgolden.com";

    public async Task SendAlertAsync(ServiceHealthResult result, CancellationToken cancellationToken = default)
    {
        var recipientEmail = options.Value.RecipientEmail!;
        var message = new EmailMessage
        {
            From = From,
            Subject = $"[ALERT] {result.Name} is {result.Status}",
            HtmlBody = $"""
                <h2 style="color:#dc3545;">Service Alert</h2>
                <p><strong>Service:</strong> {result.Name}</p>
                <p><strong>Status:</strong> {result.Status}</p>
                <p><strong>Details:</strong> {result.Description ?? "No details available"}</p>
                <p><strong>Detected at:</strong> {result.CheckedAt:O}</p>
                """,
        };
        message.To.Add(recipientEmail);
        await resend.EmailSendAsync(message, cancellationToken);
    }

    public async Task SendRecoveryAsync(ServiceHealthResult result, CancellationToken cancellationToken = default)
    {
        var recipientEmail = options.Value.RecipientEmail!;
        var message = new EmailMessage
        {
            From = From,
            Subject = $"[RECOVERY] {result.Name} is {result.Status}",
            HtmlBody = $"""
                <h2 style="color:#198754;">Service Recovery</h2>
                <p><strong>Service:</strong> {result.Name}</p>
                <p><strong>Status:</strong> {result.Status}</p>
                <p><strong>Details:</strong> {result.Description ?? "No details available"}</p>
                <p><strong>Recovered at:</strong> {result.CheckedAt:O}</p>
                """,
        };
        message.To.Add(recipientEmail);
        await resend.EmailSendAsync(message, cancellationToken);
    }
}
