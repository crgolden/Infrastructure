namespace Infrastructure.Tests.Controllers;

using Infrastructure.Controllers;
using Infrastructure.Models;
using Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

[Trait("Category", "Unit")]
public sealed class StatusControllerTests
{
    [Fact]
    public void Get_WhenSnapshotExists_ReturnsOkWithSnapshot()
    {
        var snapshot = new HealthSnapshot(
            DateTimeOffset.UtcNow,
            [new ServiceHealthResult("SQL Server", ServiceStatus.Healthy, "Connected", DateTimeOffset.UtcNow)]);

        var service = new Mock<IHealthMonitorService>();
        service.Setup(s => s.LastSnapshot).Returns(snapshot);

        var controller = new StatusController(service.Object);
        var result = controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(snapshot, ok.Value);
    }

    [Fact]
    public void Get_WhenNoSnapshotYet_Returns503()
    {
        var service = new Mock<IHealthMonitorService>();
        service.Setup(s => s.LastSnapshot).Returns((HealthSnapshot?)null);

        var controller = new StatusController(service.Object);
        var result = controller.Get();

        var statusResult = Assert.IsType<StatusCodeResult>(result.Result);
        Assert.Equal(503, statusResult.StatusCode);
    }
}
