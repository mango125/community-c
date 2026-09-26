using Community_C.Models;
using Community_C.Models.ViewModels;
using Community_C.Utility.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Community_C.Controllers
{
    public class BoardController : Controller
    {
        private readonly ILogger<BoardController> _logger;
        private readonly DataContext _db;
        public BoardController(ILogger<BoardController> logger, DataContext db)
        {
            _logger = logger;
            _db = db;
        }
        public async Task<IActionResult> Read(int id, bool increment = true)
        {
            Board? board = await _db.board
                .Include(b => b.user)
                .FirstOrDefaultAsync(b => b.id == id);

            if (board is null)
            {
                return NotFound();
            }

            if (increment)
            {
                board.view += 1;
                await _db.SaveChangesAsync();
            }

            ViewBag.comments = await _db.comment
                .Include(c => c.user)
                .Where(c => c.board_id == id)
                .ToListAsync();

            return View(board);
        }

        [Authorize]
        [AccessTokenOnly]
        public async Task<IActionResult> Write(int id = -1)
        {
            string? userId = GetAuthenticatedUserId();
            if (userId is null)
            {
                return Unauthorized();
            }

            if (!await HasBoardWritePermissionAsync(userId))
            {
                return Forbid();
            }

            if (id <= 0)
            {
                return View();
            }

            Board? board = await _db.board
                .Include(b => b.user)
                .SingleOrDefaultAsync(b => b.id == id);

            if (board is null)
            {
                return NotFound();
            }

            return board.user_id == userId ? View(board) : Forbid();
        }

        [HttpPost]
        [Authorize]
        [AccessTokenOnly]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> WriteBoard(string title, string content)
        {
            string? userId = GetAuthenticatedUserId();
            if (userId is null)
            {
                return Unauthorized();
            }

            if (!await HasBoardWritePermissionAsync(userId))
            {
                return Forbid();
            }

            title = title?.Trim() ?? string.Empty;
            content = content?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
            {
                return BadRequest("제목과 내용을 입력해주세요.");
            }

            var board = new Board
            {
                title = title,
                content = content,
                date = DateTime.UtcNow,
                user_id = userId
            };

            _db.board.Add(board);
            await _db.SaveChangesAsync();

            return RedirectToAction("Read", new { id = board.id, increment = false });
        }

        [HttpPost]
        [Authorize]
        [AccessTokenOnly]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBoard(int id, string title, string content)
        {
            string? userId = GetAuthenticatedUserId();
            if (userId is null)
            {
                return Unauthorized();
            }

            if (!await HasBoardWritePermissionAsync(userId))
            {
                return Forbid();
            }

            Board? board = await _db.board.SingleOrDefaultAsync(item => item.id == id);
            if (board is null)
            {
                return NotFound();
            }

            if (board.user_id != userId)
            {
                return Forbid();
            }

            title = title?.Trim() ?? string.Empty;
            content = content?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
            {
                return BadRequest("제목과 내용을 입력해주세요.");
            }

            board.title = title;
            board.content = content;
            await _db.SaveChangesAsync();

            return RedirectToAction("Read", new { id = board.id, increment = false });
        }

        [HttpPost]
        [Authorize]
        [AccessTokenOnly]
        public async Task<IActionResult> WriteComment([FromBody] Comment model)
        {
            string? userId = GetAuthenticatedUserId();
            if (userId is null)
            {
                return Unauthorized();
            }

            if (!await HasBoardWritePermissionAsync(userId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    success = false,
                    message = "게시판 읽기 전용 계정입니다."
                });
            }

            string content = model.content?.Trim() ?? string.Empty;
            if (model.board_id <= 0 || string.IsNullOrWhiteSpace(content))
            {
                return BadRequest(new { success = false, message = "댓글 정보를 확인해주세요." });
            }

            if (!await _db.board.AnyAsync(board => board.id == model.board_id))
            {
                return NotFound(new { success = false, message = "게시글을 찾을 수 없습니다." });
            }

            int? parentCommentId = null;
            int depth = 0;
            if (model.comment_id.HasValue)
            {
                bool parentExists = await _db.comment.AnyAsync(comment =>
                    comment.id == model.comment_id.Value && comment.board_id == model.board_id);
                if (!parentExists)
                {
                    return BadRequest(new { success = false, message = "답글 대상을 확인해주세요." });
                }

                parentCommentId = model.comment_id;
                depth = 1;
            }

            var comment = new Comment
            {
                user_id = userId,
                board_id = model.board_id,
                content = content,
                date = DateTime.UtcNow,
                comment_id = parentCommentId,
                depth = depth
            };

            _db.comment.Add(comment);
            await _db.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        [Authorize]
        [AccessTokenOnly]
        public async Task<IActionResult> CheckBoardAuthor([FromBody] BoardUserViewModel model)
        {
            string? userId = GetAuthenticatedUserId();
            if (userId is null)
            {
                return Unauthorized();
            }

            if (!await HasBoardWritePermissionAsync(userId))
            {
                return Forbid();
            }

            Board? board = await _db.board.FirstOrDefaultAsync(item => item.id == model.board_id);
            if (board is null)
            {
                return NotFound(new { success = false, message = "게시글을 찾을 수 없습니다." });
            }

            return board.user_id == userId
                ? Json(new { success = true })
                : Forbid();
        }

        [HttpPost]
        [Authorize]
        [AccessTokenOnly]
        public async Task<IActionResult> SendRecommendBoard([FromBody] Recommend request)
        {
            string? userId = GetAuthenticatedUserId();
            if (userId is null)
            {
                return Unauthorized();
            }

            if (!await HasBoardWritePermissionAsync(userId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    success = false,
                    message = "게시판 읽기 전용 계정입니다."
                });
            }

            if (!request.board_id.HasValue)
            {
                return BadRequest(new { success = false, message = "게시글 정보를 확인해주세요." });
            }

            int boardId = request.board_id.Value;
            Board? board = await _db.board.FirstOrDefaultAsync(item => item.id == boardId);
            if (board is null)
            {
                return NotFound(new { success = false, message = "게시글을 찾을 수 없습니다." });
            }

            Recommend? existingRecommend = await _db.recommend
                .FirstOrDefaultAsync(item => item.user_id == userId && item.board_id == boardId);

            if (existingRecommend is not null)
            {
                _db.recommend.Remove(existingRecommend);
                board.recommend = Math.Max(0, board.recommend - 1);
                await _db.SaveChangesAsync();
                return Json(new { success = true, message = "추천이 취소되었습니다." });
            }

            _db.recommend.Add(new Recommend { user_id = userId, board_id = boardId });
            board.recommend += 1;
            await _db.SaveChangesAsync();
            return Json(new { success = true, message = "추천되었습니다!" });
        }

        private string? GetAuthenticatedUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        private async Task<bool> HasBoardWritePermissionAsync(string userId)
        {
            string? permission = await _db.user
                .AsNoTracking()
                .Where(user => user.id == userId)
                .Select(user => user.permission)
                .SingleOrDefaultAsync();

            return BoardPermissions.CanWrite(permission);
        }
    }
}
