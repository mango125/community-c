using Community_C.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Community_C.Utility;

public static class JsonWebJoken
{
    public static List<string> CreateJwtToken(
        string id,
        string name,
        JwtSettings jwtSettings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(jwtSettings);

        Claim[] commonClaims =
        [
            new Claim(JwtRegisteredClaimNames.Sub, id),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Name, name)
        ];

        IEnumerable<Claim> accessClaims = commonClaims.Append(
            new Claim(
                AuthTokenConstants.TokenTypeClaim,
                AuthTokenConstants.AccessTokenType));
        IEnumerable<Claim> refreshClaims = commonClaims.Append(
            new Claim(
                AuthTokenConstants.TokenTypeClaim,
                AuthTokenConstants.RefreshTokenType));

        return
        [
            WriteToken(
                accessClaims,
                DateTime.UtcNow.AddMinutes(jwtSettings.AccessTokenExpiryMinutes),
                jwtSettings),
            WriteToken(
                refreshClaims,
                DateTime.UtcNow.AddDays(jwtSettings.RefreshExpiryDays),
                jwtSettings)
        ];
    }

    public static string GenerateAccessToken(
        IEnumerable<Claim> claims,
        JwtSettings jwtSettings)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(jwtSettings);

        IEnumerable<Claim> accessTokenClaims = claims
            .Where(claim =>
                claim.Type != AuthTokenConstants.TokenTypeClaim &&
                claim.Type != JwtRegisteredClaimNames.Exp &&
                claim.Type != JwtRegisteredClaimNames.Nbf &&
                claim.Type != JwtRegisteredClaimNames.Iat)
            .Append(new Claim(
                AuthTokenConstants.TokenTypeClaim,
                AuthTokenConstants.AccessTokenType));

        return WriteToken(
            accessTokenClaims,
            DateTime.UtcNow.AddMinutes(jwtSettings.AccessTokenExpiryMinutes),
            jwtSettings);
    }

    public static ClaimsPrincipal? GetPrincipalFromExpiredRefreshToken(
        string token,
        JwtSettings jwtSettings)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            ClaimsPrincipal principal = tokenHandler.ValidateToken(
                token,
                CreateValidationParameters(jwtSettings, validateLifetime: false),
                out SecurityToken validatedToken);

            if (validatedToken is not JwtSecurityToken jwtToken ||
                !string.Equals(
                    jwtToken.Header.Alg,
                    SecurityAlgorithms.HmacSha256,
                    StringComparison.Ordinal))
            {
                return null;
            }

            string? tokenType = principal
                .FindFirst(AuthTokenConstants.TokenTypeClaim)?
                .Value;

            return tokenType == AuthTokenConstants.RefreshTokenType
                ? principal
                : null;
        }
        catch (SecurityTokenException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public static bool IsExpired(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        string? expirationValue = principal
            .FindFirst(JwtRegisteredClaimNames.Exp)?
            .Value;

        return !long.TryParse(expirationValue, out long expirationSeconds) ||
               DateTimeOffset.FromUnixTimeSeconds(expirationSeconds) <=
               DateTimeOffset.UtcNow;
    }

    public static TokenValidationParameters CreateAccessTokenValidationParameters(
        JwtSettings jwtSettings)
    {
        return CreateValidationParameters(jwtSettings, validateLifetime: true);
    }

    private static TokenValidationParameters CreateValidationParameters(
        JwtSettings jwtSettings,
        bool validateLifetime)
    {
        ArgumentNullException.ThrowIfNull(jwtSettings);

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = validateLifetime,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = CreateSecurityKey(jwtSettings),
            ClockSkew = TimeSpan.Zero
        };
    }

    private static string WriteToken(
        IEnumerable<Claim> claims,
        DateTime expires,
        JwtSettings jwtSettings)
    {
        var token = new JwtSecurityToken(
            issuer: jwtSettings.Issuer,
            audience: jwtSettings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: new SigningCredentials(
                CreateSecurityKey(jwtSettings),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static SymmetricSecurityKey CreateSecurityKey(
        JwtSettings jwtSettings)
    {
        if (string.IsNullOrWhiteSpace(jwtSettings.Key))
        {
            throw new InvalidOperationException(
                "JwtSettings:Key configuration is required.");
        }

        return new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings.Key));
    }
}
