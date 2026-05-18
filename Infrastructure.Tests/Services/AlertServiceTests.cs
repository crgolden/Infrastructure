namespace Infrastructure.Tests.Services;

using Azure.Messaging.ServiceBus;
using Infrastructure.Services;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;
using Models;
using Moq;

[Trait("Category", "Unit")]
public sealed class AlertServiceTests
{
    [Fact]
    public async Task SendAlertAsync_SendsEmailWithAlertSubject()
    {
        var (service, senderMock) = BuildService();
        var result = new ServiceHealthResult("SQL Server", ServiceStatus.Unhealthy, "Connection refused", DateTimeOffset.UtcNow);

        await service.SendAlertAsync(result, CancellationToken.None);

        senderMock.Verify(
            s => s.SendMessageAsync(
                It.Is<ServiceBusMessage>(m =>
                    m.Subject.Contains("ALERT") &&
                    m.Subject.Contains("SQL Server") &&
                    m.To == "admin@example.com" &&
                    m.ReplyTo == "noreply@crgolden.com"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendRecoveryAsync_SendsEmailWithRecoverySubject()
    {
        var (service, senderMock) = BuildService();
        var result = new ServiceHealthResult("SQL Server", ServiceStatus.Healthy, "Connected", DateTimeOffset.UtcNow);

        await service.SendRecoveryAsync(result, CancellationToken.None);

        senderMock.Verify(
            s => s.SendMessageAsync(
                It.Is<ServiceBusMessage>(m =>
                    m.Subject.Contains("RECOVERY") &&
                    m.Subject.Contains("SQL Server") &&
                    m.To == "admin@example.com" &&
                    m.ReplyTo == "noreply@crgolden.com"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAlertAsync_WhenSenderThrows_PropagatesException()
    {
        var (service, senderMock) = BuildService();
        senderMock
            .Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus error"));

        var result = new ServiceHealthResult("Redis", ServiceStatus.Unhealthy, "Timeout", DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendAlertAsync(result, CancellationToken.None));
    }

    private static (AlertService service, Mock<ServiceBusSender> senderMock) BuildService()
    {
        var senderMock = new Mock<ServiceBusSender>();
        senderMock
            .Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var factoryMock = new Mock<IAzureClientFactory<ServiceBusSender>>();
        factoryMock.Setup(f => f.CreateClient("email")).Returns(senderMock.Object);

        var options = Options.Create(new AlertOptions { RecipientEmail = "admin@example.com" });
        return (new AlertService(factoryMock.Object, options), senderMock);
    }
}
