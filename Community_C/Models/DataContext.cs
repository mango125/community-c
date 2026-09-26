using Microsoft.EntityFrameworkCore;

namespace Community_C.Models
{
    public class DataContext : DbContext
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {

        }
        public DbSet<Board> board { get; set; }
        public DbSet<User> user { get; set; }
        public DbSet<User_Site> user_site { get; set; }
        public DbSet<User_Social> user_social { get; set; }
        public DbSet<Comment> comment { get; set; }
        public DbSet<Recommend> recommend { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 자체 로그인 정보
            modelBuilder.Entity<User_Site>()
                .HasOne(us => us.user)
                .WithOne(u => u.user_Site)
                .HasForeignKey<User_Site>(us => us.user_id)
                .OnDelete(DeleteBehavior.Cascade);

            // 소셜 로그인 정보
            modelBuilder.Entity<User_Social>()
                .HasOne(us => us.user)
                .WithMany(u => u.user_Socials)
                .HasForeignKey(us => us.user_id)
                .OnDelete(DeleteBehavior.Cascade);

            // 댓글 작성자
            modelBuilder.Entity<Comment>()
                .HasOne(c => c.user)
                .WithMany()
                .HasForeignKey(c => c.user_id)
                .OnDelete(DeleteBehavior.Restrict);

            // 댓글이 속한 게시글
            modelBuilder.Entity<Comment>()
                .HasOne(c => c.board)
                .WithMany()
                .HasForeignKey(c => c.board_id)
                .OnDelete(DeleteBehavior.Cascade);

            // 추천 복합 기본 키
            modelBuilder.Entity<Recommend>()
                .HasKey(r => new { r.user_id, r.board_id });

            // 게시글 작성자
            modelBuilder.Entity<Board>()
                .HasOne(b => b.user)
                .WithMany()
                .HasForeignKey(b => b.user_id)
                .OnDelete(DeleteBehavior.Restrict);

            // 동일한 소셜 계정 중복 등록 방지
            modelBuilder.Entity<User_Social>()
                .HasIndex(us => new { us.provider, us.provider_user_id })
                .IsUnique();
        }
    }
}
