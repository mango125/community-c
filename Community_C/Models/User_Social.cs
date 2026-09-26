using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Community_C.Models
{
    public class User_Social
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int social_id { get; set; } //PK
        [MaxLength(36)]
        public string user_id { get; set; } = null!; //FK
        public string provider { get; set; } = null!;
        public string provider_user_id { get; set; } = null!;

        // Navigation Property
        public User user { get; set; } = null!;

    }
}
