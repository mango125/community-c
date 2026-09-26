namespace Community_C.Models
{
    public class OAuthModel
    {
        interface IOAuth
        {
            public string Id { get; set; }
            public string Secret { get; set; }
            public string Redirect_URL { get; set; }
        }
        public class OAuth_Naver : IOAuth
        {
            public string Id { get; set; }
            public string Secret { get; set; }
            public string Redirect_URL { get; set; }
        }
        public class OAuth_Kakao : IOAuth
        {
            public string Id { get; set; }
            public string Secret { get; set; }
            public string Redirect_URL { get; set; }
        }
        public class OAuth_Google : IOAuth
        {
            public string Id { get; set; }
            public string Secret { get; set; }
            public string Redirect_URL { get; set; }
        }

    }
}
