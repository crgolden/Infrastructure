namespace Infrastructure.Services;

using Microsoft.Extensions.Options;
using Models;
using Resend;

public sealed class AlertService : IAlertService
{
    private const string From = "noreply@crgolden.com";

    private readonly IResend _resend;
    private readonly string _to;

    public AlertService(IServiceScopeFactory serviceScopeFactory, IOptions<AlertOptions> options)
    {
        var scope = serviceScopeFactory.CreateScope();
        _resend = scope.ServiceProvider.GetRequiredService<IResend>();
        if (IsNullOrWhiteSpace(options.Value.RecipientEmail))
        {
            throw new InvalidOperationException("Invalid 'RecipientEmail'.");
        }

        _to = options.Value.RecipientEmail;
    }

    public async Task SendAlertAsync(ServiceHealthResult result, CancellationToken cancellationToken = default)
    {
        var htmlBody = $"""
                        <h2 style="color:#dc3545;">Service Alert</h2>
                        <p><strong>Service:</strong> {result.Name}</p>
                        <p><strong>Status:</strong> {result.Status}</p>
                        <p><strong>Details:</strong> {result.Description ?? "No details available"}</p>
                        <p><strong>Detected at:</strong> {result.CheckedAt:O}</p>
                        """;
        await SendEmailAsync(
            subject: $"[ALERT] {result.Name} is {result.Status}",
            htmlBody: htmlBody,
            cancellationToken: cancellationToken);
    }

    public async Task SendRecoveryAsync(ServiceHealthResult result, CancellationToken cancellationToken = default)
    {
        var htmlBody = $"""
                        <h2 style="color:#198754;">Service Recovery</h2>
                        <p><strong>Service:</strong> {result.Name}</p>
                        <p><strong>Status:</strong> {result.Status}</p>
                        <p><strong>Details:</strong> {result.Description ?? "No details available"}</p>
                        <p><strong>Recovered at:</strong> {result.CheckedAt:O}</p>
                        """;
        await SendEmailAsync(
            subject: $"[RECOVERY] {result.Name} is {result.Status}",
            htmlBody: htmlBody,
            cancellationToken: cancellationToken);
    }

    private async Task SendEmailAsync(
        string subject,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        var message = new EmailMessage { From = From, Subject = subject, HtmlBody = htmlBody };
        message.To.Add(_to);
        await _resend.EmailSendAsync(message, cancellationToken);
    }
}
