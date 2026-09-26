namespace Community_C.Models
{
    // OAuth API 응답 모델 interface
    interface IApiResponse
    {
        string access_token { get; set; }
        string response_token { get; set; }
    }
    public class Naver_Api : IApiResponse
    {
        public string access_token { get; set; }
        public string response_token { get; set; }

    }
    public class Kakao_Api : IApiResponse
    {
        public string access_token { get; set; }
        public string response_token { get; set; }
        public int expires_in { get; set; } // access token 유효기간(초) 엑세스 토큰과 ID 토큰의 만료 시간은 동일
        public string refresh_token { get; set; } // refresh token
        public int refresh_token_expires_in { get; set; } // refresh token 유효기간(초)

    }
    public class Google_Api : IApiResponse
    {
        public string access_token { get; set; }
        public string response_token { get; set; }

    }
}
