namespace Community_C.Models
{
    public class Comment
    {
        public int? id { get; set; }
        public string? user_id { get; set; }
        public int board_id { get; set; }
        public string content { get; set; }
        private DateTime _date;
        public DateTime date
        {
            get => _date;
            set => _date = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
        public User? user { get; set; }
        public Board? board { get; set; }
        public int? comment_id { get; set; }
        public int depth { get; set; }
    }
}
