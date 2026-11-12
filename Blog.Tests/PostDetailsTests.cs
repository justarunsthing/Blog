using System.Security.Claims;
using Blog.Controllers;
using Blog.Data;
using Blog.Enums;
using Blog.Models;
using Blog.Services;
using Blog.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Blog.Tests;

public class PostDetailsTests
{
    [Fact]
    public async Task Details_opens_a_production_post_for_anyone()
    {
        await using var database = new TestDatabase();
        await database.AddPostAsync("public-note", ReadyStatus.Production, "author");
        var controller = database.CreateController();

        var result = await controller.Details("public-note");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PostDetailViewModel>(view.Model);
        Assert.Equal(ReadyStatus.Production, model.Post.Status);
        Assert.Equal("public-note", model.Post.Slug);
    }

    [Fact]
    public async Task Details_lists_tags_from_production_posts_only()
    {
        await using var database = new TestDatabase();
        await database.AddPostAsync("public-note", ReadyStatus.Production, "author", "shipped");
        await database.AddPostAsync("draft-note", ReadyStatus.Incomplete, "author", "secret");
        await database.AddPostAsync("preview-note", ReadyStatus.Preview, "author", "soon");

        var result = await database.CreateController().Details("public-note");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PostDetailViewModel>(view.Model);
        Assert.Equal(new[] { "shipped" }, model.Tags);
    }

    [Fact]
    public async Task Details_opens_a_preview_post_for_anyone()
    {
        await using var database = new TestDatabase();
        await database.AddPostAsync("preview-note", ReadyStatus.Preview, "author");
        var controller = database.CreateController();

        var result = await controller.Details("preview-note");

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Details_hides_an_incomplete_post_from_everyone_else()
    {
        await using var database = new TestDatabase();
        await database.AddPostAsync("draft-note", ReadyStatus.Incomplete, "author");
        var controller = database.CreateController();

        var result = await controller.Details("draft-note");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_opens_an_incomplete_post_for_its_author_or_an_administrator()
    {
        await using var database = new TestDatabase();
        await database.AddPostAsync("draft-note", ReadyStatus.Incomplete, "author");

        var authorResult = await database.CreateController("author").Details("draft-note");
        var administratorResult = await database.CreateController(role: BlogRole.Administrator.ToString()).Details("draft-note");

        Assert.IsType<ViewResult>(authorResult);
        Assert.IsType<ViewResult>(administratorResult);
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ApplicationDbContext _context;

        public TestDatabase()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;
            _context = new ApplicationDbContext(options);
            _context.Database.EnsureCreated();
        }

        public async Task AddPostAsync(string slug, ReadyStatus status, string authorId, string tag = null)
        {
            if (!await _context.Users.AnyAsync(user => user.Id == authorId))
            {
                _context.Users.Add(new BlogUser
                {
                    Id = authorId,
                    UserName = authorId,
                    NormalizedUserName = authorId.ToUpperInvariant(),
                    Email = authorId + "@example.com",
                    NormalizedEmail = (authorId + "@example.com").ToUpperInvariant(),
                    FirstName = "Ann",
                    LastName = "Author",
                    SecurityStamp = Guid.NewGuid().ToString()
                });
            }

            var blog = new Blog.Models.Blog
            {
                Name = "Notes",
                Description = "Notes on the work",
                Created = DateTime.UtcNow,
                AuthorId = authorId,
                ImageData = new byte[] { 1 },
                ContentType = "image/png"
            };
            _context.Blogs.Add(blog);
            await _context.SaveChangesAsync();

            var post = new Post
            {
                BlogId = blog.Id,
                AuthorId = authorId,
                Title = slug,
                Abstract = "A short abstract",
                Content = "The post body",
                Created = DateTime.UtcNow,
                Status = status,
                Slug = slug,
                ImageData = new byte[] { 1 },
                ContentType = "image/png"
            };
            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            if (tag != null)
            {
                _context.Tags.Add(new Tag
                {
                    PostId = post.Id,
                    AuthorId = authorId,
                    Text = tag
                });
                await _context.SaveChangesAsync();
            }
        }

        public PostsController CreateController(string userId = null, string role = null)
        {
            var controller = new PostsController(
                _context,
                new SlugService(_context),
                new StubImageService(),
                CreateUserManager(),
                new SearchService(_context));

            var claims = new List<Claim>();
            if (userId != null)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
            }

            if (role != null)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, claims.Count == 0 ? null : "test"))
                }
            };

            return controller;
        }

        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private UserManager<BlogUser> CreateUserManager()
        {
            return new UserManager<BlogUser>(
                new UserStore<BlogUser>(_context),
                Options.Create(new IdentityOptions()),
                new PasswordHasher<BlogUser>(),
                new List<IUserValidator<BlogUser>> { new UserValidator<BlogUser>() },
                new List<IPasswordValidator<BlogUser>> { new PasswordValidator<BlogUser>() },
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                null,
                NullLogger<UserManager<BlogUser>>.Instance);
        }
    }
}
