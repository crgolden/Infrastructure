namespace Infrastructure.Services;

using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;
using Models;

public sealed class AlertService : IAlertService
{
    private const string From = "noreply@crgolden.com";
    private readonly string _to;
    private readonly ServiceBusClient _serviceBusClient;

    public AlertService(IAzureClientFactory<ServiceBusClient> serviceBusClientFactory, IOptions<AlertOptions> options)
    {
        if (IsNullOrWhiteSpace(options.Value.RecipientEmail))
        {
            throw new InvalidOperationException($"Invalid '{nameof(AlertOptions.RecipientEmail)}'.");
        }

        _to = options.Value.RecipientEmail;
        _serviceBusClient = serviceBusClientFactory.CreateClient("crgolden");
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
        var message = new ServiceBusMessage(htmlBody)
        {
            ReplyTo = From,
            Subject = subject,
            To = _to
        };
        var serviceBusSender = _serviceBusClient.CreateSender("email");
        await serviceBusSender.SendMessageAsync(message, cancellationToken);
    }
}
