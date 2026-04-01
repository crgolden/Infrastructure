namespace Infrastructure.Tests.Hubs;

using Infrastructure.Hubs;

[Trait("Category", "Unit")]
public sealed class HealthHubTests
{
    [Fact]
    public void HealthHub_IsSignalRHub()
    {
        var hub = new HealthHub();
        Assert.IsAssignableFrom<Microsoft.AspNetCore.SignalR.Hub>(hub);
    }
}
