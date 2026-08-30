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
        // Arrange
        var recipientAddress = $"{Guid.NewGuid()}@test.invalid";
        var unhealthyServiceName = $"service-{Guid.NewGuid()}";
        var failureDetail = $"detail-{Guid.NewGuid()}";
        var expectedSubject = $"{AlertService.AlertSubjectPrefix} {unhealthyServiceName} is {ServiceStatus.Unhealthy}";
        var (service, senderMock) = BuildService(recipientAddress);
        var result = new ServiceHealthResult(
            unhealthyServiceName,
            ServiceStatus.Unhealthy,
            failureDetail,
            DateTimeOffset.UtcNow);

        // Act
        await service.SendAlertAsync(result, TestContext.Current.CancellationToken);

        // Assert
        senderMock.Verify(
            s => s.SendMessageAsync(
                It.Is<ServiceBusMessage>(m =>
                    string.Equals(m.Subject, expectedSubject, StringComparison.Ordinal) &&
                    string.Equals(m.To, recipientAddress, StringComparison.Ordinal) &&
                    string.Equals(m.ReplyTo, AlertService.From, StringComparison.Ordinal) &&
                    m.Body.ToString().Contains(unhealthyServiceName, StringComparison.Ordinal) &&
                    m.Body.ToString().Contains(failureDetail, StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendRecoveryAsync_SendsEmailWithRecoverySubject()
    {
        // Arrange
        var recipientAddress = $"{Guid.NewGuid()}@test.invalid";
        var recoveredServiceName = $"service-{Guid.NewGuid()}";
        var recoveryDetail = $"detail-{Guid.NewGuid()}";
        var expectedSubject = $"{AlertService.RecoverySubjectPrefix} {recoveredServiceName} is {ServiceStatus.Healthy}";
        var (service, senderMock) = BuildService(recipientAddress);
        var result = new ServiceHealthResult(
            recoveredServiceName,
            ServiceStatus.Healthy,
            recoveryDetail,
            DateTimeOffset.UtcNow);

        // Act
        await service.SendRecoveryAsync(result, TestContext.Current.CancellationToken);

        // Assert
        senderMock.Verify(
            s => s.SendMessageAsync(
                It.Is<ServiceBusMessage>(m =>
                    string.Equals(m.Subject, expectedSubject, StringComparison.Ordinal) &&
                    string.Equals(m.To, recipientAddress, StringComparison.Ordinal) &&
                    string.Equals(m.ReplyTo, AlertService.From, StringComparison.Ordinal) &&
                    m.Body.ToString().Contains(recoveredServiceName, StringComparison.Ordinal) &&
                    m.Body.ToString().Contains(recoveryDetail, StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAlertAsync_WhenSenderThrows_PropagatesException()
    {
        // Arrange
        var recipientAddress = $"{Guid.NewGuid()}@test.invalid";
        var unhealthyServiceName = $"service-{Guid.NewGuid()}";
        var failureDetail = $"detail-{Guid.NewGuid()}";
        var transportFailureMessage = $"service-bus-failure-{Guid.NewGuid()}";
        var (service, senderMock) = BuildService(recipientAddress);
        senderMock
            .Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(transportFailureMessage));

        var result = new ServiceHealthResult(
            unhealthyServiceName,
            ServiceStatus.Unhealthy,
            failureDetail,
            DateTimeOffset.UtcNow);

        // Act
        var thrown = await Record.ExceptionAsync(() =>
            service.SendAlertAsync(result, TestContext.Current.CancellationToken));

        // Assert
        var propagated = Assert.IsType<InvalidOperationException>(thrown);
        Assert.Equal(transportFailureMessage, propagated.Message);
    }

    [Fact]
    public void Constructor_WhenRecipientEmailIsMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        var factoryMock = new Mock<IAzureClientFactory<ServiceBusClient>>(MockBehavior.Strict);

        // Act
        var thrown = Record.Exception(() => new AlertService(
            factoryMock.Object,
            Options.Create(new AlertOptions { RecipientEmail = null })));

        // Assert
        Assert.IsType<InvalidOperationException>(thrown);
    }

    private static (AlertService Service, Mock<ServiceBusSender> SenderMock) BuildService(string recipientAddress)
    {
        var senderMock = new Mock<ServiceBusSender>(MockBehavior.Strict);
        senderMock
            .Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var clientMock = new Mock<ServiceBusClient>(MockBehavior.Strict);
        clientMock.Setup(c => c.CreateSender(AlertService.EmailQueueName)).Returns(senderMock.Object);

        var factoryMock = new Mock<IAzureClientFactory<ServiceBusClient>>(MockBehavior.Strict);
        factoryMock.Setup(f => f.CreateClient(AlertService.ServiceBusClientName)).Returns(clientMock.Object);

        var options = Options.Create(new AlertOptions { RecipientEmail = recipientAddress });
        return (new AlertService(factoryMock.Object, options), senderMock);
    }
}
