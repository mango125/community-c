using System.ComponentModel.DataAnnotations;

namespace Community_C.Models
{
    public class User
    {
        [Key]
        [MaxLength(36)]
        public string id { get; set; }
        public string name { get; set; }
        public string email { get; set; }
        public string type { get; set; }
        public string? refreshKey { get; set; } = null;
        public string status { get; set; }
        [MaxLength(20)]
        public string? permission { get; set; } = string.Empty;
        private DateTime _createDate { get; set; }
        public DateTime createDate
        {
            get => _createDate;
            set => _createDate = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
        public User_Site? user_Site { get; set; } 
        public ICollection<User_Social> user_Socials { get; set; } = new List<User_Social>();
    }
}
