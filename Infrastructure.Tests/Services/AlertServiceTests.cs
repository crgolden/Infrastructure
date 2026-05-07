namespace Infrastructure.Tests.Services;

using Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Models;
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

        var service = new AlertService(BuildScopeFactory(resend.Object), GetDefaultOptions());
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

        var service = new AlertService(BuildScopeFactory(resend.Object), GetDefaultOptions());
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

        var service = new AlertService(BuildScopeFactory(resend.Object), GetDefaultOptions());
        var result = new ServiceHealthResult("Redis", ServiceStatus.Unhealthy, "Timeout", DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendAlertAsync(result, CancellationToken.None));
    }

    private static IServiceScopeFactory BuildScopeFactory(IResend resend)
    {
        var sp = new Mock<IServiceProvider>();
        sp.Setup(x => x.GetService(typeof(IResend))).Returns(resend);

        var scope = new Mock<IServiceScope>();
        scope.Setup(x => x.ServiceProvider).Returns(sp.Object);

        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(x => x.CreateScope()).Returns(scope.Object);

        return factory.Object;
    }

    private static IOptions<AlertOptions> GetDefaultOptions() => Options.Create(new AlertOptions { RecipientEmail = "admin@example.com" });
}
