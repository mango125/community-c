using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Community_C.Models;
using Community_C.Utility;
using Microsoft.Extensions.Options;

namespace Community_C.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TokenController : ControllerBase
    {
        private readonly ITokenService _tokenService; // JWT 토큰 서비스 인터페이스
        public TokenController(ITokenService tokenService)
        {
            _tokenService = tokenService;
        }

        /// <summary>
        /// refresh 토큰검증 api
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("refresh")]
        public IActionResult RefreshToken()
        {

            // 쿠키에서 refresh token 읽기
            if (!Request.Cookies.TryGetValue("refreshJWT", out var refreshToken))
            {
                return Unauthorized("Refresh token not found.");
            }

            // refresh token 검증
            var principal = _tokenService.GetPrincipalFromExpiredToken(refreshToken);
            
            if (principal != null)
            {
                //refresh 토큰으로 스토리지(db검사)
                var subClaim = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
                if (subClaim is null || string.IsNullOrWhiteSpace(subClaim.Value))
                {
                    return Unauthorized("Invalid refresh token.");
                }

                string userId = subClaim.Value;
                var storedRefreshToken = _tokenService.GetStoredRefreshToken(userId, refreshToken);
                if (storedRefreshToken)
                {
                    if (_tokenService.IsRefreshTokenExpired(principal))
                    {
                        return Unauthorized("Refresh token expired.");
                    }
                    else
                    {
                        // 새로운 access token 생성
                        var newAccessToken = _tokenService.GenerateAccessToken(principal.Claims);

                        return Ok(new
                        {
                            accessToken = newAccessToken
                        });
                    }

                }
                //유효하지 않은 토큰입니다 return
            }
            return Unauthorized("Invalid refresh token.");

        }
    }

    public interface ITokenService
    {
        ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
        string GenerateAccessToken(IEnumerable<Claim> claims);
        bool IsRefreshTokenExpired(ClaimsPrincipal principal);
        bool GetStoredRefreshToken(string userId, string refreshToken);
    }
    public class TokenService : ITokenService
    {
        private readonly JwtSettings _jwtSettings;

        private readonly DataContext _db;
        public TokenService(IOptions<JwtSettings> jwtSettings, DataContext db)
        {
            _jwtSettings = jwtSettings.Value;
            _db = db;
        }

        public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            return JsonWebJoken.GetPrincipalFromExpiredRefreshToken(
                token,
                _jwtSettings);
        }

        public string GenerateAccessToken(IEnumerable<Claim> claims)
        {
            return JsonWebJoken.GenerateAccessToken(claims, _jwtSettings);
        }
        /// <summary>
        /// 토큰의 유효기간 검사
        /// </summary>
        /// <param name="storedRefreshToken"></param>
        /// <returns></returns>
        public bool IsRefreshTokenExpired(ClaimsPrincipal principal)
        {
            return JsonWebJoken.IsExpired(principal);
        }

        public bool GetStoredRefreshToken(string userId, string refreshToken)
        {
            string? hashed = Encryption.HashRefreshToken(refreshToken);
            if (hashed is null)
            {
                return false;
            }

            var user = _db.user.FirstOrDefault(rt => rt.id == userId && rt.refreshKey == hashed);
            if (user != null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
