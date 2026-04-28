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
        await SendEmailAsync(
            subject: $"[ALERT] {result.Name} is {result.Status}",
            htmlBody: $"""
                <h2 style="color:#dc3545;">Service Alert</h2>
                <p><strong>Service:</strong> {result.Name}</p>
                <p><strong>Status:</strong> {result.Status}</p>
                <p><strong>Details:</strong> {result.Description ?? "No details available"}</p>
                <p><strong>Detected at:</strong> {result.CheckedAt:O}</p>
                """,
            activityName: "infrastructure.resend.send_alert",
            activityType: "alert",
            result: result,
            cancellationToken: cancellationToken);
    }

    public async Task SendRecoveryAsync(ServiceHealthResult result, CancellationToken cancellationToken = default)
    {
        await SendEmailAsync(
            subject: $"[RECOVERY] {result.Name} is {result.Status}",
            htmlBody: $"""
                <h2 style="color:#198754;">Service Recovery</h2>
                <p><strong>Service:</strong> {result.Name}</p>
                <p><strong>Status:</strong> {result.Status}</p>
                <p><strong>Details:</strong> {result.Description ?? "No details available"}</p>
                <p><strong>Recovered at:</strong> {result.CheckedAt:O}</p>
                """,
            activityName: "infrastructure.resend.send_recovery",
            activityType: "recovery",
            result: result,
            cancellationToken: cancellationToken);
    }

    private async Task SendEmailAsync(
        string subject,
        string htmlBody,
        string activityName,
        string activityType,
        ServiceHealthResult result,
        CancellationToken cancellationToken)
    {
        var message = new EmailMessage { From = From, Subject = subject, HtmlBody = htmlBody };
        message.To.Add(_to);

        using var activity = Telemetry.ActivitySource.StartActivity(activityName);
        activity?.SetTag("alert.service_name", result.Name);
        activity?.SetTag("alert.type", activityType);
        await _resend.EmailSendAsync(message, cancellationToken);
    }
}
