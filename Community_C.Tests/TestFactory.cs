using Community_C.Models;
using Community_C.Utility.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Community_C.Tests;

internal static class TestFactory
{
    public static DataContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new DataContext(options);
    }

    public static void Authenticate(Controller controller, string userId)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(AuthTokenConstants.TokenTypeClaim, AuthTokenConstants.AccessTokenType)
            },
            "TestAuthentication");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }

    public static User CreateUser(
        string id,
        string email,
        string name = "사용자",
        string? permission = BoardPermissions.Writer)
    {
        return new User
        {
            id = id,
            email = email,
            name = name,
            type = "site",
            status = "active",
            permission = permission,
            createDate = DateTime.UtcNow
        };
    }

    public static JwtSettings CreateJwtSettings()
    {
        return new JwtSettings
        {
            Issuer = "Community_C.Tests",
            Audience = "Community_C.Tests",
            Key = "test-signing-key-with-at-least-32-bytes",
            AccessTokenExpiryMinutes = 30,
            RefreshExpiryDays = 7
        };
    }
}
