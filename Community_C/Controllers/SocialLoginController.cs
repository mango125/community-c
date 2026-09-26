using Community_C.Models;
using Community_C.Utility;
using Community_C.Utility.Logs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Text.Json;

namespace Community_C.Controllers
{
    public class SocialLoginController : Controller
    {
        private readonly OAuthModel.OAuth_Naver _naver;
        private readonly OAuthModel.OAuth_Kakao _kakao;
        private readonly OAuthModel.OAuth_Google _google;
        private readonly DataContext _db;
        private readonly JwtSettings _jwtSettings;
        private readonly ILogger<SocialLoginController> _logger;
        public SocialLoginController(
        IOptions<OAuthModel.OAuth_Naver> naverSettings,
        IOptions<OAuthModel.OAuth_Kakao> kakaoSettings,
        IOptions<OAuthModel.OAuth_Google> googleSettings,
        DataContext db,
        IOptions<JwtSettings> jwt,
        ILogger<SocialLoginController> logger)
        {
            _naver = naverSettings.Value;
            _kakao = kakaoSettings.Value;
            _google = googleSettings.Value;
            _db = db;
            _jwtSettings = jwt.Value;
            _logger = logger;
        }

        /// <summary>
        /// Authorization_Naver
        /// </summary>
        /// <returns></returns>
        public IActionResult OAuthNaver()
        {
            string clientId = _naver.Id; // 클라이언트 ID
            string redirectUri = _naver.Redirect_URL; // 인증 후 리다이렉션될 URI
            string state = CreateOAuthState("naver");

            //string authorizationUrl = $"https://nid.naver.com/oauth2.0/token?response_type=code&client_id={clientId}&state={state}&redirect_uri={redirectUri}";
            string authorizationUrl = $"https://nid.naver.com/oauth2.0/authorize?response_type=code&client_id={clientId}&state={state}&redirect_uri={redirectUri}";

            // 클라이언트를 인증 페이지로 리디렉션
            return Redirect(authorizationUrl);
        }
        /// <summary>
        /// Authorization_Kakao
        /// </summary>
        /// <returns></returns>
        public IActionResult OAuthKakao()
        {
            string clientId = _kakao.Id; // 클라이언트 ID
            string redirectUri = _kakao.Redirect_URL; // 인증 후 리다이렉션될 URI
            string state = CreateOAuthState("kakao");

            string authorizationUrl = $"https://kauth.kakao.com/oauth/authorize?response_type=code&client_id={clientId}&state={state}&redirect_uri={redirectUri}";

            // 클라이언트를 인증 페이지로 리디렉션
            return Redirect(authorizationUrl);
        }
        /// <summary>
        /// Authorization_Google
        /// </summary>
        /// <returns></returns>
        public IActionResult OAuthGoogle()
        {
            string clientId = _google.Id; //  클라이언트 ID
            string redirectUri = _google.Redirect_URL; // 인증 후 리다이렉션될 URI
            string state = CreateOAuthState("google");

            string authorizationUrl = $"https://accounts.google.com/o/oauth2/auth?" +
                                        $"client_id={clientId}&" +
                                         $"redirect_uri={redirectUri}&" +
                                         $"response_type=code&" +
                                         $"scope=https://www.googleapis.com/auth/userinfo.email https://www.googleapis.com/auth/userinfo.profile&" +
                                         $"state={state}";

            // 클라이언트를 인증 페이지로 리디렉션
            return Redirect(authorizationUrl);
        }

        /// <summary>
        /// GetLoginToken_Naver
        /// </summary>
        /// <param name="code"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        public async Task<IActionResult> OAuthNaverLogin(string code, string state)
        {
            if (!ValidateOAuthState("naver", state))
            {
                return BadRequest("Invalid OAuth state.");
            }

            HttpClient _httpClient = new HttpClient();
            try
            {
                // 요청 URL 구성
                string requestUri = $"https://nid.naver.com/oauth2.0/token" +
                                    $"?grant_type=authorization_code" +
                                    $"&client_id={_naver.Id}" +
                                    $"&client_secret={_naver.Secret}" +
                                    $"&code={code}" +
                                    $"&state={state}";

                // HTTP GET 요청 전송
                HttpResponseMessage response = await _httpClient.GetAsync(requestUri);

                // 요청이 성공적이지 않은 경우 예외 던짐
                response.EnsureSuccessStatusCode();

                // 응답 본문을 문자열로 읽기
                string responseBody = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<Naver_Api>(responseBody);

                // 가져온 토큰으로 사용자 인증하기
                string access_token = apiResponse.access_token;
                return await OAuthLogin(access_token, "naver");
            }
            catch (HttpRequestException e)
            {
                ServerLog.OAuthRemoteRequestFailed(_logger, "naver", "token exchange", GetStatusCode(e));
                return BadRequest();
            }
        }
        /// <summary>
        /// GetLoginToken_Kakao
        /// </summary>
        /// <param name="code"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        public async Task<IActionResult> OAuthKakaoLogin(string code, string state)
        {
            if (!ValidateOAuthState("kakao", state))
            {
                return BadRequest("Invalid OAuth state.");
            }

            HttpClient _httpClient = new HttpClient();
            try
            {
                // 요청 URL 구성
                string requestUri = $"https://kauth.kakao.com/oauth/token" +
                                    $"?grant_type=authorization_code" +
                                    $"&client_id={_kakao.Id}" +
                                    $"&redirect_uri={_kakao.Redirect_URL}" +
                                    $"&code={code}" +
                                    $"&client_secret={_kakao.Secret}";

                // HTTP GET 요청 전송
                HttpResponseMessage response = await _httpClient.GetAsync(requestUri);

                // 요청이 성공적이지 않은 경우 예외 던짐
                response.EnsureSuccessStatusCode();

                // 응답 본문을 문자열로 읽기
                string responseBody = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<Kakao_Api>(responseBody);

                // 가져온 토큰으로 사용자 인증하기
                string access_token = apiResponse.access_token;
                return await OAuthLogin(access_token, "kakao");
            }
            catch (HttpRequestException e)
            {
                ServerLog.OAuthRemoteRequestFailed(_logger, "kakao", "token exchange", GetStatusCode(e));
                return BadRequest();
            }
        }

        /// <summary>
        /// GetLoginToken_Google
        /// </summary>
        /// <param name="code"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        public async Task<IActionResult> OAuthGoogleLogin(string code, string state)
        {
            if (!ValidateOAuthState("google", state))
            {
                return BadRequest("Invalid OAuth state.");
            }

            using (HttpClient _httpClient = new HttpClient())
            {
                try
                {
                    // 요청 URL 구성
                    string requestUri = "https://oauth2.googleapis.com/token";

                    // 요청 본문 구성
                    var requestBody = new Dictionary<string, string>
                    {
                        { "code", code },
                        { "client_id", _google.Id },
                        { "client_secret", _google.Secret },
                        { "redirect_uri", _google.Redirect_URL },
                        { "grant_type", "authorization_code" }
                    };

                    // HTTP POST 요청 전송
                    HttpResponseMessage response = await _httpClient.PostAsync(requestUri, new FormUrlEncodedContent(requestBody));

                    // 요청이 성공적이지 않은 경우 예외 던짐
                    response.EnsureSuccessStatusCode();

                    // 응답 본문을 문자열로 읽기
                    string responseBody = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<Google_Api>(responseBody);

                    // 가져온 토큰으로 사용자 인증하기
                    string access_token = apiResponse.access_token;
                    return await OAuthLogin(access_token, "google"); // await 키워드 추가
                }
                catch (HttpRequestException e)
                {
                    ServerLog.OAuthRemoteRequestFailed(_logger, "google", "token exchange", GetStatusCode(e));
                    return BadRequest();
                }
            }
        }

        public async Task<IActionResult> OAuthLogin(string access_token, string sender)
        {
            /*
             * 2026.08.15 로직 변경
             * social login 시, access_token을 통해 사용자 정보를 가져오고, 해당 사용자가 이미 존재하는지 확인
             */

            string userInfoJson = "";
            if (sender == "naver")
            {
                userInfoJson = await OAuthNaverGetInfo(access_token);
            }
            else if (sender == "kakao")
            {
                userInfoJson = await OAuthKakaoGetInfo(access_token);
            }
            else if (sender == "google")
            {
                userInfoJson = await OAuthGoogleGetInfo(access_token);
            }
            else
            {
                return BadRequest("Unsupported OAuth provider.");
            }

            User user = new User();

            using (JsonDocument document = JsonDocument.Parse(userInfoJson))
            {
                JsonElement root = document.RootElement;
                //여기서 sns별 parse

                if (sender == "naver")
                {
                    user = GenerateNaverUserFromJson(root);
                }
                else if (sender == "kakao")
                {
                    user = GenerateKakaoUserFromJson(root);
                }
                else if (sender == "google")
                {
                    user = GenerateGoogleUserFromJson(root);
                }
            }

            //API로 가져온 사용자 정보를 기반으로 DB에서 해당 사용자가 존재하는지 확인
            //사용자가 없다면 null
            string? provider_user_id = user.user_Socials.FirstOrDefault()?.provider_user_id;
            if (string.IsNullOrWhiteSpace(provider_user_id))
            {
                return BadRequest("OAuth provider user id was not returned.");
            }

            User_Social? user_Social = await _db.user_social.FirstOrDefaultAsync(c =>
                c.provider == sender && c.provider_user_id == provider_user_id);

            //회원 정보가 있을 시 로그인
            if (user_Social!= null)
            {
                //DB에서 사용자 정보를 가져옴
                User? existingUser = await _db.user.FirstOrDefaultAsync(c => c.id == user_Social.user_id);
                if (existingUser is null)
                {
                    return Unauthorized();
                }

                return await CompleteOAuthLoginAsync(existingUser);

                //2026.08.17 소셜로그인 회원가입 페이지 생성으로 인하여 기존 로그인 로직 통합관리를 위하여 주석처리
                ////JWT 토큰 발급
                //string user_id = user.id;
                //string user_name = user.name;
                //// 비동기적으로 JWT 토큰을 생성합니다.
                //var token = JsonWebJoken.CreateJwtToken(user_id, user_name, _jwtSettings);
                //string accessToken = token[0];
                //string refreshToken = token[1];
                //var cookieOptions = new CookieOptions
                //{
                //    HttpOnly = true,
                //    Secure = true,          // HTTPS 요청에서만 전송되도록 설정
                //    Expires = DateTime.UtcNow.AddDays(_jwtSettings.RefreshExpiryDays),  // 만료기간
                //    SameSite = SameSiteMode.Strict
                //};
                //HttpContext.Response.Cookies.Append("refreshJWT", refreshToken, cookieOptions);
                //TempData["AccessToken"] = accessToken;
                //return RedirectToAction("OAuth", "User");
            }
            //신규 회원일 경우, 회원가입 페이지로 이동
            else
            {
                TempData["OAuthProvider"] = sender;
                TempData["OAuthProviderUserId"] = provider_user_id;
                TempData["OAuthEmail"] = user.email;
                TempData["OAuthName"] = user.name;
                return RedirectToAction("OAuthSignUp", "User");

                /*
                 * 2026.08.17
                 * 로그인 구조 개편으로 인하여 이중 검사 로직 제거 및 회원가입 페이지로 이동
                //사용자가 없을경우
                //JWT 토큰 발행
                //var token = JsonWebJoken.CreateJwtToken(user.id, user.name, _jwtSettings);

                ////정보는 있지만, 토큰이 없을 경우, 토큰만 발급
                //User user_check = _db.user.FirstOrDefault(c => c.id == user.id);
                //if (user_check != null)
                //{
                //    User_Social user_social = _db.user_social.FirstOrDefault(c => c.user_id == user.id);
                //    //user_social.accessToken = access_token;
                //    //_db.user_social.Update(user_social);
                //    //await _db.SaveChangesAsync();
                //    TempData["AccessToken"] = access_token;
                //    return RedirectToAction("OAuth", "User");
                //}
                //else
                //{
                //    _db.user.Add(user);
                //    _db.user_social.Add(user.user_Socials.First());
                //    await _db.SaveChangesAsync();
                //    return RedirectToAction("Login", "User");
                //}
                */
            }
        }

        /// <summary>
        /// 최초로 OAuth 로그인 시, 닉네임을 입력받아 회원가입 진행
        /// </summary>
        /// <param name="nickname"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OAuthSignUp([FromForm] string nickname)
        {
            nickname = nickname?.Trim();

            if (string.IsNullOrWhiteSpace(nickname))
            {
                return BadRequest("닉네임을 입력해주세요.");
            }

            string? provider = TempData.Peek("OAuthProvider")?.ToString();
            string? providerUserId = TempData.Peek("OAuthProviderUserId")?.ToString();
            string? email = TempData.Peek("OAuthEmail")?.ToString();

            if (string.IsNullOrWhiteSpace(provider) ||
                string.IsNullOrWhiteSpace(providerUserId))
            {
                return RedirectToAction("Login");
            }

            //중복 등록을 방지하기 위해, provider와 provider_user_id를 기반으로 기존 사용자를 검색
            User_Social? user_social = await _db.user_social
                .Include(s => s.user)
                .FirstOrDefaultAsync(s =>
                    s.provider == provider &&
                    s.provider_user_id == providerUserId);

            User user;

            if (user_social != null)
            {
                user = user_social.user;
            }
            else
            {
                user = CreateSocialUser(providerUserId, nickname,email, provider);

                _db.user.Add(user);
                _db.user_social.Add(user.user_Socials.First());
                await _db.SaveChangesAsync();
            }


            TempData.Remove("OAuthProvider");
            TempData.Remove("OAuthProviderUserId");
            TempData.Remove("OAuthEmail");
            TempData.Remove("OAuthName");

            //var token = JsonWebJoken.CreateJwtToken(
            //    user.id,
            //    user.name,
            //    _jwtSettings);
            //TempData["AccessToken"] = token[0];

            return await CompleteOAuthLoginAsync(user);
        }

        /// <summary>
        /// 최종 로그인을 위해 jwt토큰 생성 및 쿠키 설정 후, UserController의 OAuth 액션으로 리다이렉트
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        private async Task<IActionResult> CompleteOAuthLoginAsync(User user)
        {
            // 비동기적으로 JWT 토큰을 생성합니다 .
            List<string> token = JsonWebJoken.CreateJwtToken(
                user.id,
                user.name,
                _jwtSettings);
            string accessToken = token[0];
            string refreshToken = token[1];
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,          // HTTPS 요청에서만 전송되도록 설정
                Expires = DateTime.UtcNow.AddDays(_jwtSettings.RefreshExpiryDays),  // 만료기간
                SameSite = SameSiteMode.Strict,
                Path = "/"
            };
            user.refreshKey = Encryption.HashRefreshToken(refreshToken);
            await _db.SaveChangesAsync();
            HttpContext.Response.Cookies.Append("refreshJWT", refreshToken, cookieOptions);
            TempData["AccessToken"] = accessToken;
            return RedirectToAction("OAuth", "User");
        }

        /// <summary>
        /// GetInfo_Naver
        /// </summary>
        /// <param name="accessToken"></param>
        /// <returns></returns>
        [Route("GetInfoNaver")]
        public async Task<string> OAuthNaverGetInfo(string accessToken)
        {

            HttpClient _httpClient = new HttpClient();
            try
            {
                // 요청 URL 구성
                string requestUri = $"https://openapi.naver.com/v1/nid/me";
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                // HTTP GET 요청 전송
                HttpResponseMessage response = await _httpClient.GetAsync(requestUri);

                // 요청이 성공적이지 않은 경우 예외 던짐
                response.EnsureSuccessStatusCode();

                // 응답 본문을 문자열로 읽기
                string responseBody = await response.Content.ReadAsStringAsync();
                return responseBody;
            }
            catch (HttpRequestException e)
            {
                ServerLog.OAuthRemoteRequestFailed(_logger, "naver", "user profile", GetStatusCode(e));
                return null;
            }
        }

        /// <summary>
        /// GetInfo_Kakao
        /// </summary>
        /// <param name="accessToken"></param>
        /// <returns></returns>
        [Route("GetInfoKakao")]
        public async Task<string> OAuthKakaoGetInfo(string accessToken)
        {

            HttpClient _httpClient = new HttpClient();
            try
            {
                // 요청 URL 구성
                //string requestUri = $"https://kapi.kakao.com/v2/user/me?property_keys=[\"kakao_account.email\"]";
                //string requestUri = $"https://kapi.kakao.com/v2/user/me?property_keys=%5B%22kakao_account.email%22%5D";
                string requestUri = $"https://kapi.kakao.com/v2/user/me";

                //Header, Query 설정
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
                request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["property_keys"] = "[\"kakao_account.email\", \"kakao_account.profile\"]"
                });
                request.Content.Headers.ContentType!.CharSet = "utf-8";

                // HTTP POST 요청 전송
                using HttpResponseMessage response = await _httpClient.SendAsync(request);

                // 응답 본문을 문자열로 읽기
                string responseBody = await response.Content.ReadAsStringAsync();

                // 요청이 성공적이지 않은 경우 예외 던짐
                response.EnsureSuccessStatusCode();

                return responseBody;
            }
            catch (HttpRequestException e)
            {
                ServerLog.OAuthRemoteRequestFailed(_logger, "kakao", "user profile", GetStatusCode(e));
                return null;
            }
        }

        /// <summary>
        /// GetInfo_Google
        /// </summary>
        /// <param name="accessToken"></param>
        /// <returns></returns>
        [Route("GetInfoGoogle")]
        public async Task<string> OAuthGoogleGetInfo(string accessToken)
        {
            using (HttpClient _httpClient = new HttpClient())
            {
                try
                {
                    // 요청 URL 구성
                    string requestUri = "https://www.googleapis.com/oauth2/v3/userinfo";
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                    // HTTP GET 요청 전송
                    HttpResponseMessage response = await _httpClient.GetAsync(requestUri);

                    // 요청이 성공적이지 않은 경우 예외 던짐
                    response.EnsureSuccessStatusCode();

                    // 응답 본문을 문자열로 읽기
                    string responseBody = await response.Content.ReadAsStringAsync();
                    return responseBody;
                }
                catch (HttpRequestException e)
                {
                    ServerLog.OAuthRemoteRequestFailed(_logger, "google", "user profile", GetStatusCode(e));
                    return null;
                }
            }
        }

        public User GenerateNaverUserFromJson(JsonElement root)
        {
            if (root.TryGetProperty("response", out JsonElement responseElement))
            {
                string id = responseElement.GetProperty("id").GetString();
                string name = responseElement.GetProperty("nickname").GetString();
                string email = responseElement.GetProperty("email").GetString();

                //User 객체 생성
                User user = CreateSocialUser(id, name, email, "naver");
                return user;
            }
            return null;
        }
        public User GenerateKakaoUserFromJson(JsonElement root)
        {
            try
            {
                string id = root.GetProperty("id").GetRawText();
                //string id = root.GetProperty("id").GetInt32().ToString();
                JsonElement account = root.GetProperty("kakao_account");
                string name = account.GetProperty("profile").GetProperty("nickname").GetString();
                string email = account.GetProperty("email").GetString();

                //User 객체 생성
                User user = CreateSocialUser(id, name, email, "kakao");
                return user;
            }
            catch(Exception e)
            {
                ServerLog.OAuthResponseInvalid(_logger, "kakao", e.GetType().Name);
                return null;
            }
        }
        public User GenerateGoogleUserFromJson(JsonElement root)
        {
            string id = root.GetProperty("sub").GetString();
            string name = root.GetProperty("given_name").GetString();
            string email = root.GetProperty("email").GetString();

            //User 객체 생성
            User user = CreateSocialUser(id, name, email, "google");
            return user;
        }
        /// <summary>
        /// CreateSocialUser OAuth API로 가져온 사용자 정보를 기반으로 User 객체를 생성
        /// 단, DB에 존재한다면 중복 등록이 되기 때문에 user.social_id를 기반으로 검색 후 사용
        /// </summary>
        /// <param name="social_id"></param>
        /// <param name="name"></param>
        /// <param name="email"></param>
        /// <param name="provider"></param>
        /// <returns></returns>
        private static int? GetStatusCode(HttpRequestException exception)
        {
            return exception.StatusCode.HasValue
                ? (int)exception.StatusCode.Value
                : null;
        }

        public User CreateSocialUser(string social_id, string name, string email, string provider)
        {
            User user = new User();
            string newId = Guid.NewGuid().ToString();
            user.id = newId;
            user.name = name;
            user.email = email;
            user.type = "social";
            user.status = "active";
            user.permission = string.Empty;
            user.createDate = DateTime.UtcNow;

            User_Social user_social = new User_Social();
            user_social.user_id = newId;
            user_social.provider = provider;
            user_social.provider_user_id = social_id;
            user.user_Socials.Add(user_social);
            return user;
        }

        private string CreateOAuthState(string provider)
        {
            string state = Encryption.CreateRandomUrlSafeToken();
            Response.Cookies.Append(GetOAuthStateCookieName(provider), state, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromMinutes(10),
                Path = "/"
            });

            return state;
        }

        private bool ValidateOAuthState(string provider, string? suppliedState)
        {
            string cookieName = GetOAuthStateCookieName(provider);
            if (!Request.Cookies.TryGetValue(cookieName, out string? expectedState) ||
                string.IsNullOrWhiteSpace(expectedState) ||
                string.IsNullOrWhiteSpace(suppliedState))
            {
                return false;
            }

            Response.Cookies.Delete(cookieName, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/"
            });

            return Encryption.FixedTimeEquals(expectedState, suppliedState);
        }

        private static string GetOAuthStateCookieName(string provider)
        {
            return $"oauth_state_{provider}";
        }
    }
}
