using Community_C.Controllers;
using Community_C.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Community_C.Tests;

public class OAuthStateSecurityTests
{
    [Fact]
    public void OAuthStart_GeneratesRandomStateAndSecureCookie()
    {
        using DataContext db = TestFactory.CreateDbContext();
        SocialLoginController firstController = CreateController(db);
        SocialLoginController secondController = CreateController(db);

        var firstRedirect = Assert.IsType<RedirectResult>(firstController.OAuthNaver());
        var secondRedirect = Assert.IsType<RedirectResult>(secondController.OAuthNaver());

        string firstState = GetQueryValue(firstRedirect.Url!, "state");
        string secondState = GetQueryValue(secondRedirect.Url!, "state");
        Assert.NotEqual(firstState, secondState);
        Assert.True(firstState.Length >= 32);

        string setCookie = firstController.Response.Headers.SetCookie.ToString();
        Assert.Contains("oauth_state_naver=", setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OAuthCallback_RejectsMissingStateCookieBeforeTokenExchange()
    {
        using DataContext db = TestFactory.CreateDbContext();
        SocialLoginController controller = CreateController(db);

        IActionResult result = await controller.OAuthNaverLogin("authorization-code", "untrusted-state");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static SocialLoginController CreateController(DataContext db)
    {
        var controller = new SocialLoginController(
            Options.Create(new OAuthModel.OAuth_Naver
            {
                Id = "naver-client",
                Secret = "naver-secret",
                Redirect_URL = "https://localhost/callback/naver"
            }),
            Options.Create(new OAuthModel.OAuth_Kakao
            {
                Id = "kakao-client",
                Secret = "kakao-secret",
                Redirect_URL = "https://localhost/callback/kakao"
            }),
            Options.Create(new OAuthModel.OAuth_Google
            {
                Id = "google-client",
                Secret = "google-secret",
                Redirect_URL = "https://localhost/callback/google"
            }),
            db,
            Options.Create(TestFactory.CreateJwtSettings()),
            NullLogger<SocialLoginController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }

    private static string GetQueryValue(string url, string key)
    {
        string query = new Uri(url).Query.TrimStart('?');
        return query.Split('&')
            .Select(pair => pair.Split('=', 2))
            .Single(pair => pair[0] == key)[1];
    }
}
