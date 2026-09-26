using Community_C.Controllers;
using Community_C.Models;
using Community_C.Models.ViewModels;
using Community_C.Utility.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Community_C.Tests;

public class BoardControllerSecurityTests
{
    [Fact]
    public async Task WriteBoard_UsesAuthenticatedUserId()
    {
        await using DataContext db = TestFactory.CreateDbContext();
        db.user.Add(TestFactory.CreateUser("writer", "writer@example.com"));
        await db.SaveChangesAsync();
        BoardController controller = CreateController(db, "writer");

        IActionResult result = await controller.WriteBoard(" 제목 ", " 내용 ");

        Assert.IsType<RedirectToActionResult>(result);
        Board board = await db.board.SingleAsync();
        Assert.Equal("writer", board.user_id);
        Assert.Equal("제목", board.title);
        Assert.Equal("내용", board.content);
    }

    [Fact]
    public async Task UpdateBoard_RejectsAnotherUsersRequest()
    {
        await using DataContext db = TestFactory.CreateDbContext();
        db.user.Add(TestFactory.CreateUser("attacker", "attacker@example.com"));
        db.board.Add(new Board
        {
            id = 1,
            user_id = "owner",
            title = "원본",
            content = "원본 내용",
            date = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        BoardController controller = CreateController(db, "attacker");

        IActionResult result = await controller.UpdateBoard(1, "변조", "변조 내용");

        Assert.IsType<ForbidResult>(result);
        Board board = await db.board.SingleAsync();
        Assert.Equal("원본", board.title);
        Assert.Equal("원본 내용", board.content);
    }

    [Fact]
    public async Task CheckBoardAuthor_IgnoresClientSuppliedUserId()
    {
        await using DataContext db = TestFactory.CreateDbContext();
        db.user.Add(TestFactory.CreateUser("attacker", "attacker@example.com"));
        db.board.Add(new Board
        {
            id = 1,
            user_id = "owner",
            title = "제목",
            content = "내용",
            date = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        BoardController controller = CreateController(db, "attacker");

        IActionResult result = await controller.CheckBoardAuthor(new BoardUserViewModel
        {
            board_id = 1,
            user_id = "owner"
        });

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task WriteComment_OverridesClientControlledIdentityAndDepth()
    {
        await using DataContext db = TestFactory.CreateDbContext();
        db.user.Add(TestFactory.CreateUser("commenter", "commenter@example.com"));
        db.board.Add(new Board
        {
            id = 1,
            user_id = "owner",
            title = "제목",
            content = "내용",
            date = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        BoardController controller = CreateController(db, "commenter");

        IActionResult result = await controller.WriteComment(new Comment
        {
            board_id = 1,
            user_id = "forged-user",
            content = " 댓글 ",
            depth = 99
        });

        Assert.IsType<JsonResult>(result);
        Comment comment = await db.comment.SingleAsync();
        Assert.Equal("commenter", comment.user_id);
        Assert.Equal(0, comment.depth);
        Assert.Equal("댓글", comment.content);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("reader")]
    [InlineData("Writer")]
    public async Task BoardWriteActions_RejectUsersWithoutExactWriterPermission(string? permission)
    {
        await using DataContext db = TestFactory.CreateDbContext();
        db.user.Add(TestFactory.CreateUser(
            "reader",
            "reader@example.com",
            permission: permission));
        db.board.Add(new Board
        {
            id = 1,
            user_id = "reader",
            title = "원본",
            content = "원본 내용",
            date = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        BoardController controller = CreateController(db, "reader");

        Assert.IsType<ForbidResult>(await controller.Write());
        Assert.IsType<ForbidResult>(await controller.WriteBoard("새 글", "새 내용"));
        Assert.IsType<ForbidResult>(await controller.UpdateBoard(1, "변경", "변경 내용"));
        Assert.IsType<ForbidResult>(await controller.CheckBoardAuthor(new BoardUserViewModel
        {
            board_id = 1
        }));

        ObjectResult commentResult = Assert.IsType<ObjectResult>(
            await controller.WriteComment(new Comment
            {
                board_id = 1,
                content = "댓글"
            }));
        Assert.Equal(StatusCodes.Status403Forbidden, commentResult.StatusCode);

        ObjectResult recommendResult = Assert.IsType<ObjectResult>(
            await controller.SendRecommendBoard(new Recommend { board_id = 1 }));
        Assert.Equal(StatusCodes.Status403Forbidden, recommendResult.StatusCode);

        Assert.Single(db.board);
        Assert.Equal("원본", (await db.board.SingleAsync()).title);
        Assert.Empty(db.comment);
        Assert.Empty(db.recommend);
    }

    [Theory]
    [InlineData(nameof(BoardController.WriteBoard))]
    [InlineData(nameof(BoardController.UpdateBoard))]
    public void FormWriteActions_RequireAuthorizationAndAntiforgery(string actionName)
    {
        var method = typeof(BoardController).GetMethod(actionName)!;

        Assert.NotNull(method.GetCustomAttributes(typeof(AuthorizeAttribute), true).SingleOrDefault());
        Assert.NotNull(method.GetCustomAttributes(typeof(AccessTokenOnlyAttribute), true).SingleOrDefault());
        Assert.NotNull(method.GetCustomAttributes(typeof(HttpPostAttribute), true).SingleOrDefault());
        Assert.NotNull(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true).SingleOrDefault());
    }

    private static BoardController CreateController(DataContext db, string userId)
    {
        var controller = new BoardController(NullLogger<BoardController>.Instance, db);
        TestFactory.Authenticate(controller, userId);
        return controller;
    }
}
