namespace Infrastructure.Services;

using System.Diagnostics;
using Microsoft.Extensions.Options;
using Models;
using Resend;

public sealed class AlertService : IAlertService
{
    private const string From = "noreply@crgolden.com";

    private readonly IResend _resend;
    private readonly string _to;

    public AlertService(IResend resend, IOptions<AlertOptions> options)
    {
        _resend = resend;
        if (IsNullOrWhiteSpace(options.Value.RecipientEmail))
        {
            throw new InvalidOperationException("Invalid 'RecipientEmail'.");
        }

        _to = options.Value.RecipientEmail;
    }

    public async Task SendAlertAsync(ServiceHealthResult result, CancellationToken cancellationToken = default)
    {
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
        message.To.Add(_to);

        using var activity = Telemetry.ActivitySource.StartActivity("infrastructure.resend.send_alert");
        activity?.SetTag("alert.service_name", result.Name);
        activity?.SetTag("alert.type", "alert");
        await _resend.EmailSendAsync(message, cancellationToken);
    }

    public async Task SendRecoveryAsync(ServiceHealthResult result, CancellationToken cancellationToken = default)
    {
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
        message.To.Add(_to);

        using var activity = Telemetry.ActivitySource.StartActivity("infrastructure.resend.send_recovery");
        activity?.SetTag("alert.service_name", result.Name);
        activity?.SetTag("alert.type", "recovery");
        await _resend.EmailSendAsync(message, cancellationToken);
    }
}
