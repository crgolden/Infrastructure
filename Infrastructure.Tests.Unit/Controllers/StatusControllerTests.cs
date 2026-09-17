namespace Infrastructure.Tests.Unit.Controllers;

using Infrastructure.Controllers;
using Infrastructure.HealthChecks;
using Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Models;
using Moq;

[Trait("Category", "Unit")]
public sealed class StatusControllerTests
{
    [Fact]
    public void Get_WhenSnapshotExists_ReturnsOkWithSnapshot()
    {
        // Arrange
        var snapshot = new HealthSnapshot(
            DateTimeOffset.UtcNow,
            [
                new ServiceHealthResult(
                    HealthCheckNames.SqlServer,
                    ServiceStatus.Healthy,
                    RelationalHealthCheck.HealthyDescription,
                    DateTimeOffset.UtcNow),
            ]);

        var service = new Mock<IHealthMonitorService>(MockBehavior.Strict);
        service.Setup(s => s.LastSnapshot).Returns(snapshot);

        var controller = new StatusController(service.Object);

        // Act
        var result = controller.Get();

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(snapshot, ok.Value);
    }

    [Fact]
    public void Get_WhenNoSnapshotYet_Returns503()
    {
        // Arrange
        var service = new Mock<IHealthMonitorService>(MockBehavior.Strict);
        service.Setup(s => s.LastSnapshot).Returns((HealthSnapshot?)null);

        var controller = new StatusController(service.Object);

        // Act
        var result = controller.Get();

        // Assert
        var statusResult = Assert.IsType<StatusCodeResult>(result.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusResult.StatusCode);
    }
}