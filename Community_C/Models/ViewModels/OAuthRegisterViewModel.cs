namespace Community_C.Models.ViewModels
{
    public class OAuthRegisterViewModel
    {
        //SNS 로그인 시 회원가입을 위해 필요한 정보

        //SNS 제공자(naver, kakao, google)
        public string OAuthProvider { get; set; } = string.Empty;
        //SNS에서 제공하는 고유한 사용자 ID
        public string OAuthProviderUserId { get; set; } = string.Empty;
        //SNS에서 제공하는 사용자 이메일
        public string OAuthEmail { get; set; } = string.Empty;
        //SNS에서 제공하는 사용자 닉네임
        public string OAuthName { get; set; } = string.Empty;
    }
}
