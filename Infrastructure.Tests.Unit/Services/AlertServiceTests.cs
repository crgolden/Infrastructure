namespace Infrastructure.Tests.Unit.Services;

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
                    m.Subject == "[ALERT] SQL Server is Unhealthy" &&
                    m.To == "admin@example.com" &&
                    m.ReplyTo == "noreply@crgolden.com" &&
                    m.Body.ToString().Contains("SQL Server") &&
                    m.Body.ToString().Contains("Connection refused")),
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
                    m.Subject == "[RECOVERY] SQL Server is Healthy" &&
                    m.To == "admin@example.com" &&
                    m.ReplyTo == "noreply@crgolden.com" &&
                    m.Body.ToString().Contains("SQL Server") &&
                    m.Body.ToString().Contains("Connected")),
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

    [Fact]
    public void Constructor_WhenRecipientEmailIsEmpty_ThrowsInvalidOperationException()
    {
        var factoryMock = new Mock<IAzureClientFactory<ServiceBusClient>>(MockBehavior.Strict);

        Assert.Throws<InvalidOperationException>(() => new AlertService(
            factoryMock.Object,
            Options.Create(new AlertOptions { RecipientEmail = null })));
    }

    private static (AlertService service, Mock<ServiceBusSender> senderMock) BuildService()
    {
        var senderMock = new Mock<ServiceBusSender>(MockBehavior.Strict);
        senderMock
            .Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var clientMock = new Mock<ServiceBusClient>(MockBehavior.Strict);
        clientMock.Setup(c => c.CreateSender("email")).Returns(senderMock.Object);

        var factoryMock = new Mock<IAzureClientFactory<ServiceBusClient>>(MockBehavior.Strict);
        factoryMock.Setup(f => f.CreateClient("crgolden")).Returns(clientMock.Object);

        var options = Options.Create(new AlertOptions { RecipientEmail = "admin@example.com" });
        return (new AlertService(factoryMock.Object, options), senderMock);
    }
}
