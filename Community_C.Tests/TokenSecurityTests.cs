using Community_C.Controllers;
using Community_C.Models;
using Community_C.Utility;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;

namespace Community_C.Tests;

public class TokenSecurityTests
{
    [Fact]
    public void IssuedTokens_HaveDistinctTokenTypes()
    {
        JwtSettings settings = TestFactory.CreateJwtSettings();

        List<string> tokens = JsonWebJoken.CreateJwtToken("user-1", "사용자", settings);

        var handler = new JwtSecurityTokenHandler();
        JwtSecurityToken accessToken = handler.ReadJwtToken(tokens[0]);
        JwtSecurityToken refreshToken = handler.ReadJwtToken(tokens[1]);
        Assert.Equal(
            AuthTokenConstants.AccessTokenType,
            accessToken.Claims.Single(claim => claim.Type == AuthTokenConstants.TokenTypeClaim).Value);
        Assert.Equal(
            AuthTokenConstants.RefreshTokenType,
            refreshToken.Claims.Single(claim => claim.Type == AuthTokenConstants.TokenTypeClaim).Value);
    }

    [Fact]
    public async Task RefreshValidation_RejectsAccessToken()
    {
        await using DataContext db = TestFactory.CreateDbContext();
        JwtSettings settings = TestFactory.CreateJwtSettings();
        List<string> tokens = JsonWebJoken.CreateJwtToken("user-1", "사용자", settings);
        var service = new TokenService(Options.Create(settings), db);

        Assert.Null(service.GetPrincipalFromExpiredToken(tokens[0]));
        var refreshPrincipal = service.GetPrincipalFromExpiredToken(tokens[1]);
        Assert.NotNull(refreshPrincipal);
        Assert.False(service.IsRefreshTokenExpired(refreshPrincipal));
    }
}
