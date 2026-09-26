using Community_C.Models;
using Community_C.Models.ViewModels;
using Community_C.Utility.Security;
using Community_C.Utility;
using Community_C.Utility.Logs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Community_C.Controllers
{
	public class UserController : Controller
	{
		// 암호화 해시 (IPasswordHasher<User> via DI)
		private readonly IPasswordHasher<User> _passwordHasher;
		private readonly ILogger<UserController> _logger;
		private readonly DataContext _db;
		private readonly JwtSettings _jwtSettings;
		public UserController(ILogger<UserController> logger, DataContext db, IOptions<JwtSettings> jwtSettings, IPasswordHasher<User> passwordHasher)
		{
			_logger = logger;
			_db = db;
			_jwtSettings = jwtSettings.Value;
			_passwordHasher = passwordHasher;
		}
        public IActionResult Login()
		{
            return View();
		}

        public IActionResult SignUp()
        {
            return View();
        }
        [Authorize]
        [AccessTokenOnly]
        public async Task<IActionResult> MyPage()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            User? user = await _db.user
                .AsNoTracking()
                .SingleOrDefaultAsync(u => u.id == userId);
            if (user is null)
            {
                return NotFound();
            }

            return View(user);
        }
        /// <summary>
        /// LOG OUT
        /// </summary>
        /// <returns></returns>
        //[Authorize]
        [HttpPost]
        public async Task<IActionResult> LogOut()
        {
            if (Request.Cookies.TryGetValue("refreshJWT", out string? refreshToken))
            {
                string? refreshTokenHash = Encryption.HashRefreshToken(refreshToken);
                User? user = await _db.user.FirstOrDefaultAsync(item => item.refreshKey == refreshTokenHash);
                if (user is not null)
                {
                    user.refreshKey = null;
                    await _db.SaveChangesAsync();
                }
            }

            Response.Cookies.Delete("refreshJWT", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/"
            });

            // 로그아웃 후 리디렉션
            return Ok(new { message = "Logged out successfully" });
        }

        public IActionResult OAuth()
        {
            string? accessToken = TempData["AccessToken"] as string;

            // 필요한 로직을 추가하여 View에 모델을 전달
            return View("OAuth",accessToken); // accessToken을 View로 전달
        }

        public IActionResult OAuthSignUp()
        {
            string? providerUserId =
            TempData.Peek("OAuthProviderUserId")?.ToString();

            if (string.IsNullOrWhiteSpace(providerUserId))
            {
                return RedirectToAction("Login");
            }

            var model = new OAuthRegisterViewModel
            {
                OAuthName = TempData.Peek("OAuthName")?.ToString()
                    ?? string.Empty
            };

            return View(model);
        }


        /// <summary>
        /// LOG IN
        /// </summary>
        /// <param name="id"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UserLogin([FromForm] string email, [FromForm] string password)
        {
            if (string.IsNullOrEmpty(email) ||  string.IsNullOrEmpty(password))
            {
                return BadRequest(new { success = false, message = "이메일과 비밀번호를 입력해주세요." });
            }

            User? user = await _db.user.FirstOrDefaultAsync(u => u.email == email);
            if (user is null)
            {
                return BadRequest(new { success = false, message = "ID 또는 비밀번호가 틀렸습니다." });
            }

            User_Site? userSite = await _db.user_site.FirstOrDefaultAsync(item => item.user_id == user.id);
            if (userSite is null)
            {
                return BadRequest(new { success = false, message = "ID 또는 비밀번호가 틀렸습니다." });
            }

            PasswordVerificationResult verificationResult =
                Encryption.VerifyPassword(
                    _passwordHasher,
                    user,
                    userSite.password,
                    password);
            if (verificationResult == PasswordVerificationResult.Failed)
            {
                return BadRequest(new { success = false, message = "ID 또는 비밀번호가 틀렸습니다." });
            }

            List<string> token = JsonWebJoken.CreateJwtToken(
                user.id,
                user.name,
                _jwtSettings);
            user.refreshKey = Encryption.HashRefreshToken(token[1]);
            await _db.SaveChangesAsync();

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                Expires = DateTimeOffset.UtcNow.AddDays(_jwtSettings.RefreshExpiryDays),
                SameSite = SameSiteMode.Strict,
                Path = "/"
            };
            Response.Cookies.Append("refreshJWT", token[1], cookieOptions);

            return Ok(new { accessToken = token[0] });
        }
        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            return await _db.user.Where(u => u.id == username).FirstOrDefaultAsync();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignUpInsert(string name, string password, string email)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "정보를 올바르게 입력해주세요");
                return View();
            }
            name = name.Trim();
            email = email.Trim();
            if (await _db.user.AnyAsync(user => user.email == email))
            {
                ModelState.AddModelError(nameof(email), "이미 가입된 이메일입니다.");
                return View("SignUp");
            }

            //유저 고유 아이디 생성
            string userId = Guid.NewGuid().ToString();

            //유저 생성
            var user = new User
            {
                id = userId,
                name = name,
                email = email,
                type = "site",
                refreshKey = null,
                status = "active", //active, inactive 
                permission = string.Empty,
                createDate = DateTime.Now
            };

            //비밀번호 해싱
            password = Encryption.HashPassword(_passwordHasher, user, password);

            //자체 로그인용 유저 생성
            var user_site = new User_Site
            {
                user_id = userId,
                password = password
            };

            _db.user.Add(user);
            _db.user_site.Add(user_site);
            await _db.SaveChangesAsync();
            ServerLog.LocalUserRegistered(
                _logger,
                user.id,
                HttpContext.TraceIdentifier);
            return RedirectToAction("Login", "User");
        }

        public async Task<IActionResult> CheckUserDuplicate([FromBody] string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest("아이디를 입력해주세요.");
            }

            // 아이디 중복 확인
            bool isDuplicate = await CheckUserDuplicateAsync(id);

            // 결과를 JSON 형식으로 반환
            return Json(new { isDuplicate });
        }

        private async Task<bool> CheckUserDuplicateAsync(string id)
        {
            // 데이터베이스에서 사용자 아이디 확인
            return await _db.user.AnyAsync(u => u.email == id);
        }

        [HttpPost]
        [Authorize]
        [AccessTokenOnly]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUser(string name)
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            name = name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name) || name.Length > 20)
            {
                return BadRequest("닉네임은 1자 이상 20자 이하로 입력해주세요.");
            }

            User? user = await _db.user.SingleOrDefaultAsync(item => item.id == userId);
            if (user is null)
            {
                return NotFound();
            }

            user.name = name;
            await _db.SaveChangesAsync();

            //return RedirectToAction("MyPage", new { id = user.id});
            return RedirectToAction("Index","Home");
            //return View("MyPage");
        }
    }
}
