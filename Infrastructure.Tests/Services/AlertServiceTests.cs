namespace Infrastructure.Tests.Services;

using Infrastructure.Models;
using Infrastructure.Services;
using Microsoft.Extensions.Options;
using Moq;
using Resend;

[Trait("Category", "Unit")]
public sealed class AlertServiceTests
{
    [Fact]
    public async Task SendAlertAsync_SendsEmailWithAlertSubject()
    {
        var resend = new Mock<IResend>();
        resend.Setup(r => r.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<ResendResponse<Guid>>(default!));

        var service = new AlertService(resend.Object, GetDefaultOptions());
        var result = new ServiceHealthResult("SQL Server", ServiceStatus.Unhealthy, "Connection refused", DateTimeOffset.UtcNow);

        await service.SendAlertAsync(result, CancellationToken.None);

        resend.Verify(
            r => r.EmailSendAsync(
                It.Is<EmailMessage>(m =>
                    m.Subject.Contains("ALERT") &&
                    m.Subject.Contains("SQL Server") &&
                    m.To.Contains("admin@example.com") &&
                    m.From.ToString() == "noreply@crgolden.com"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendRecoveryAsync_SendsEmailWithRecoverySubject()
    {
        var resend = new Mock<IResend>();
        resend.Setup(r => r.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<ResendResponse<Guid>>(default!));

        var service = new AlertService(resend.Object, GetDefaultOptions());
        var result = new ServiceHealthResult("SQL Server", ServiceStatus.Healthy, "Connected", DateTimeOffset.UtcNow);

        await service.SendRecoveryAsync(result, CancellationToken.None);

        resend.Verify(
            r => r.EmailSendAsync(
                It.Is<EmailMessage>(m =>
                    m.Subject.Contains("RECOVERY") &&
                    m.Subject.Contains("SQL Server") &&
                    m.To.Contains("admin@example.com") &&
                    m.From.ToString() == "noreply@crgolden.com"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAlertAsync_WhenResendThrows_PropagatesException()
    {
        var resend = new Mock<IResend>();
        resend.Setup(r => r.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Resend API error"));

        var service = new AlertService(resend.Object, GetDefaultOptions());
        var result = new ServiceHealthResult("Redis", ServiceStatus.Unhealthy, "Timeout", DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendAlertAsync(result, CancellationToken.None));
    }

    private static IOptions<AlertOptions> GetDefaultOptions() => Options.Create(new AlertOptions { RecipientEmail = "admin@example.com" });
}
