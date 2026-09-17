namespace Infrastructure.Tests.Unit.Pages;

using Infrastructure.Pages;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;

[Trait("Category", "Unit")]
public sealed class LogoutTests
{
    [Fact]
    public void OnPost_ReturnsSignOutResult_WithBothSchemes()
    {
        // Arrange
        var model = new LogoutModel();

        // Act
        var result = model.OnPost();

        // Assert
        var signOutResult = Assert.IsType<SignOutResult>(result);
        Assert.Contains(CookieAuthenticationDefaults.AuthenticationScheme, signOutResult.AuthenticationSchemes);
        Assert.Contains(OpenIdConnectDefaults.AuthenticationScheme, signOutResult.AuthenticationSchemes);
    }

    [Fact]
    public void OnPost_ReturnsSignOutResult_WithRedirectToRoot()
    {
        // Arrange
        var model = new LogoutModel();

        // Act
        var result = model.OnPost();

        // Assert
        var signOutResult = Assert.IsType<SignOutResult>(result);
        Assert.Equal(LogoutModel.SignedOutRedirectUri, signOutResult.Properties?.RedirectUri);
    }
}