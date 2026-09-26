using Community_C.Controllers;
using Community_C.Models;
using Community_C.Utility;
using Community_C.Utility.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Community_C.Tests;

public class UserControllerSecurityTests
{
    [Fact]
    public async Task SignUpInsert_CreatesReadOnlyUserByDefault()
    {
        await using DataContext db = TestFactory.CreateDbContext();
        UserController controller = CreateController(db);

        IActionResult result = await controller.SignUpInsert(
            "신규 사용자",
            "password",
            "new-user@example.com");

        Assert.IsType<RedirectToActionResult>(result);
        User user = await db.user.SingleAsync();
        Assert.Equal(string.Empty, user.permission);
        Assert.False(BoardPermissions.CanWrite(user.permission));
    }

    [Fact]
    public async Task UpdateUser_UpdatesOnlyAuthenticatedUser()
    {
        await using DataContext db = TestFactory.CreateDbContext();
        db.user.AddRange(
            TestFactory.CreateUser("current-user", "current@example.com", "기존 이름"),
            TestFactory.CreateUser("other-user", "other@example.com", "다른 사용자"));
        await db.SaveChangesAsync();
        UserController controller = CreateController(db, "current-user");

        IActionResult result = await controller.UpdateUser(" 새 이름 ");

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("새 이름", (await db.user.FindAsync("current-user"))!.name);
        Assert.Equal("다른 사용자", (await db.user.FindAsync("other-user"))!.name);
    }

    [Fact]
    public async Task UserLogin_IssuesSecureRefreshCookieAndStoresOnlyHash()
    {
        await using DataContext db = TestFactory.CreateDbContext();
        var passwordHasher = new PasswordHasher<User>();
        User user = TestFactory.CreateUser("user-1", "user@example.com");
        db.user.Add(user);
        db.user_site.Add(new User_Site
        {
            user_id = user.id,
            user = user,
            password = Encryption.HashPassword(passwordHasher, user, "correct-password")
        });
        await db.SaveChangesAsync();
        UserController controller = CreateController(db, null, passwordHasher);

        IActionResult result = await controller.UserLogin(user.email, "correct-password");

        Assert.IsType<OkObjectResult>(result);
        Assert.False(string.IsNullOrWhiteSpace(user.refreshKey));
        string setCookie = controller.Response.Headers.SetCookie.ToString();
        Assert.Contains("refreshJWT=", setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LogOut_RevokesMatchingRefreshTokenHash()
    {
        await using DataContext db = TestFactory.CreateDbContext();
        const string refreshToken = "refresh-token-value";
        User user = TestFactory.CreateUser("user-1", "user@example.com");
        user.refreshKey = Encryption.HashRefreshToken(refreshToken);
        db.user.Add(user);
        await db.SaveChangesAsync();
        UserController controller = CreateController(db);
        controller.Request.Headers.Cookie = $"refreshJWT={refreshToken}";

        IActionResult result = await controller.LogOut();

        Assert.IsType<OkObjectResult>(result);
        Assert.Null(user.refreshKey);
        Assert.Contains("refreshJWT=", controller.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public void UpdateUser_RequiresAuthorizationAndAntiforgery()
    {
        var method = typeof(UserController).GetMethod(nameof(UserController.UpdateUser))!;

        Assert.NotNull(method.GetCustomAttributes(typeof(AuthorizeAttribute), true).SingleOrDefault());
        Assert.NotNull(method.GetCustomAttributes(typeof(AccessTokenOnlyAttribute), true).SingleOrDefault());
        Assert.NotNull(method.GetCustomAttributes(typeof(HttpPostAttribute), true).SingleOrDefault());
        Assert.NotNull(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true).SingleOrDefault());
    }

    private static UserController CreateController(
        DataContext db,
        string? userId = null,
        IPasswordHasher<User>? passwordHasher = null)
    {
        var controller = new UserController(
            NullLogger<UserController>.Instance,
            db,
            Options.Create(TestFactory.CreateJwtSettings()),
            passwordHasher ?? new PasswordHasher<User>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        if (userId is not null)
        {
            TestFactory.Authenticate(controller, userId);
        }

        return controller;
    }
}
