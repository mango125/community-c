using Community_C.Models;
using Community_C.Utility.Logs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Diagnostics;

namespace Community_C.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly DataContext _db;
        public HomeController(ILogger<HomeController> logger, DataContext db)
        {
            _logger = logger;
            _db = db;
        }

        public IActionResult Index(int page = 1)
        {
            // 페이지당 보여줄 게시글 수
            int pageSize = 15;

            int totalRecords = _db.board.Count();
            int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
            List<Board> boards = new List<Board>();
            try
            {
                // 게시글을 최신 날짜 기준으로 정렬하고 페이징 처리
                boards = _db.board
                    .OrderByDescending(board => board.id) // 'CreatedDate'는 게시글 생성일 필드라고 가정
                    .Skip((page - 1) * pageSize) // 해당 페이지의 시작점으로 건너뛰기
                    .Take(pageSize) // 한 페이지에 표시할 데이터 수
                    .Include(b => b.user)
                    .ToList();
            }
            catch (Exception ex)
            {
                ServerLog.BoardListLoadFailed(_logger, ex);
            }
            ViewData["TotalPages"] = totalPages;
            ViewData["CurrentPage"] = page;

            return View(boards);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
