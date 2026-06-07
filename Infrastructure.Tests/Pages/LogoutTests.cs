namespace Infrastructure.Tests.Pages;

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
        var model = new LogoutModel();

        var result = model.OnPost();

        var signOutResult = Assert.IsType<SignOutResult>(result);
        Assert.Contains(CookieAuthenticationDefaults.AuthenticationScheme, signOutResult.AuthenticationSchemes);
        Assert.Contains(OpenIdConnectDefaults.AuthenticationScheme, signOutResult.AuthenticationSchemes);
    }

    [Fact]
    public void OnPost_ReturnsSignOutResult_WithRedirectToRoot()
    {
        var model = new LogoutModel();

        var result = model.OnPost();

        var signOutResult = Assert.IsType<SignOutResult>(result);
        Assert.Equal("/", signOutResult.Properties?.RedirectUri);
    }
}
