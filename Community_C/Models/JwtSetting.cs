namespace Community_C.Models
{
    public class JwtSettings
    {
            public string Issuer { get; set; }
            public string Audience { get; set; }
            public string Key { get; set; }
            public int AccessTokenExpiryMinutes { get; set; }
            public int RefreshExpiryDays { get; set; }

    }
}
