using System.ComponentModel.DataAnnotations;

namespace Community_C.Models
{
    public class User_Site
    {
        [Key]
        [MaxLength(36)]
        // Foreign Key to User
        /*
         * 일반 사용자는 email과 password를 통해 로그인하지만, 
         * 소셜 로그인은 email이 없고, 소셜 로그인 시에는 password가 필요하지 않다. 
         * 따라서 User_Site 테이블을 만들어서 일반 사용자와 소셜 사용자를 구분한다.
        */
        public string user_id { get; set; } = null!; //PK, FK
        [DataType(DataType.Password)]
        public string password { get; set; } = null!;

        // Navigation Property
        public User user { get; set; } = null!;

    }
}
