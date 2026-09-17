namespace Infrastructure.Tests.Unit.Hubs;

using Infrastructure.Hubs;

[Trait("Category", "Unit")]
public sealed class HealthHubTests
{
    [Fact]
    public void HealthHub_IsSignalRHub()
    {
        // Act
        var hub = new HealthHub();

        // Assert
        Assert.IsType<Microsoft.AspNetCore.SignalR.Hub>(hub, exactMatch: false);
    }
}