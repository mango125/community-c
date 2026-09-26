using System.ComponentModel.DataAnnotations;

namespace Community_C.Models
{
    public class Board
    {
        //public Board()
        //{
        //    this.date = DateTime.SpecifyKind(date, DateTimeKind.Utc);
        //}
        [Key]
        public int id { get; set; }
        public string title{ get; set; }
        public string content { get; set; }
        private DateTime _date;
        public DateTime date
        {
            get => _date;
            set => _date = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
        public int view { get; set; }
        public string? user_id { get; set; }
        public int recommend { get; set; }
        public User? user { get; set; }
    }
}
