namespace Infrastructure.Tests.Unit.Extensions;

using Infrastructure.Extensions;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Moq;

[Trait("Category", "Unit")]
public sealed class HttpContextExtensionsTests
{
    [Fact]
    public void ListeningAddressUri_WhenAnHttpsAddressIsListed_PrefersItOverAnEarlierHttpAddress()
    {
        // Arrange
        var httpAddress = Generated.NewListeningAddress(Uri.UriSchemeHttp);
        var httpsAddress = Generated.NewListeningAddress(Uri.UriSchemeHttps);
        var callbackPath = Generated.NewCallbackPath();
        var httpContext = ContextListeningOn(httpAddress, httpsAddress);

        // Act
        var result = httpContext.ListeningAddressUri(callbackPath);

        // Assert
        Assert.Equal(new Uri($"{httpsAddress}{callbackPath}"), result);
    }

    [Fact]
    public void ListeningAddressUri_WhenOnlyAnHttpAddressIsListed_UsesIt()
    {
        // Arrange
        var httpAddress = Generated.NewListeningAddress(Uri.UriSchemeHttp);
        var callbackPath = Generated.NewCallbackPath();
        var httpContext = ContextListeningOn(httpAddress);

        // Act
        var result = httpContext.ListeningAddressUri(callbackPath);

        // Assert
        Assert.Equal(new Uri($"{httpAddress}{callbackPath}"), result);
    }

    [Fact]
    public void ListeningAddressUri_WhenTheAddressEndsWithASlash_DoesNotDoubleIt()
    {
        // Arrange
        var httpsAddress = Generated.NewListeningAddress(Uri.UriSchemeHttps);
        var callbackPath = Generated.NewCallbackPath();
        var httpContext = ContextListeningOn($"{httpsAddress}/");

        // Act
        var result = httpContext.ListeningAddressUri(callbackPath);

        // Assert
        Assert.Equal(new Uri($"{httpsAddress}{callbackPath}"), result);
    }

    [Fact]
    public void ListeningAddressUri_WhenNoAddressIsListed_ReturnsNull()
    {
        // Arrange
        var callbackPath = Generated.NewCallbackPath();
        var httpContext = ContextListeningOn();

        // Act
        var result = httpContext.ListeningAddressUri(callbackPath);

        // Assert
        Assert.Null(result);
    }

    private static DefaultHttpContext ContextListeningOn(params string[] addresses)
    {
        var addressesFeature = new ServerAddressesFeature();
        foreach (var address in addresses)
        {
            addressesFeature.Addresses.Add(address);
        }

        var features = new FeatureCollection();
        features.Set<IServerAddressesFeature>(addressesFeature);
        var server = new Mock<IServer>(MockBehavior.Strict);
        server.Setup(s => s.Features).Returns(features);
        var services = new Mock<IServiceProvider>(MockBehavior.Strict);
        services.Setup(p => p.GetService(typeof(IServer))).Returns(server.Object);
        return new DefaultHttpContext { RequestServices = services.Object };
    }
}
