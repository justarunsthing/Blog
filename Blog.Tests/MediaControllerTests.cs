using System.Security.Claims;
using Blog.Controllers;
using Blog.Data;
using Blog.Enums;
using Blog.Interfaces;
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
using X.PagedList;

namespace Blog.Tests;

public class MediaControllerTests
{
    private static readonly byte[] SampleImage = { 9, 8, 7 };

    [Fact]
    public async Task Post_returns_the_bytes_and_content_type_for_a_production_post()
    {
        await using var database = new TestDatabase();
        var postId = (await database.AddPostAsync("public-note", ReadyStatus.Production, "author")).PostId;
        var controller = database.CreateMediaController();

        var result = await controller.Post(postId);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal(SampleImage, file.FileContents);
        Assert.Equal("image/png", file.ContentType);
        Assert.Equal("public, max-age=86400", controller.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task Post_returns_not_found_when_the_post_has_no_image()
    {
        await using var database = new TestDatabase();
        var postId = (await database.AddPostAsync("public-note", ReadyStatus.Production, "author", includeImage: false)).PostId;
        var controller = database.CreateMediaController();

        var result = await controller.Post(postId);

        Assert.IsType<NotFoundResult>(result);
        Assert.IsType<NotFoundResult>(await controller.Post(postId + 100));
    }

    [Fact]
    public async Task Post_hides_an_incomplete_image_from_anonymous_users()
    {
        await using var database = new TestDatabase();
        var postId = (await database.AddPostAsync("draft-note", ReadyStatus.Incomplete, "author")).PostId;

        var anonymous = await database.CreateMediaController().Post(postId);
        var authorController = database.CreateMediaController("author");
        var author = await authorController.Post(postId);
        var administrator = await database.CreateMediaController(role: BlogRole.Administrator.ToString()).Post(postId);
        var previewId = (await database.AddPostAsync("preview-note", ReadyStatus.Preview, "author")).PostId;
        var previewController = database.CreateMediaController();
        var preview = await previewController.Post(previewId);

        Assert.IsType<NotFoundResult>(anonymous);
        var authorFile = Assert.IsType<FileContentResult>(author);
        var administratorFile = Assert.IsType<FileContentResult>(administrator);
        var previewFile = Assert.IsType<FileContentResult>(preview);
        Assert.Equal(SampleImage, authorFile.FileContents);
        Assert.Equal("image/png", authorFile.ContentType);
        Assert.Equal("private, max-age=86400", authorController.Response.Headers.CacheControl);
        Assert.Equal(SampleImage, administratorFile.FileContents);
        Assert.Equal(SampleImage, previewFile.FileContents);
        Assert.Equal("public, max-age=86400", previewController.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task List_queries_page_without_selecting_image_bytes()
    {
        await using var database = new TestDatabase();
        var (postId, blogId) = await database.AddPostAsync("public-note", ReadyStatus.Production, "author", "shipped");

        var sql = new SearchService(database.Context).Search("body").ToQueryString();
        var selectClause = sql.Split(new[] { "WHERE" }, StringSplitOptions.None)[0];
        Assert.DoesNotContain("ImageData", selectClause, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"Content\"", selectClause, StringComparison.OrdinalIgnoreCase);

        var postsController = database.CreatePostsController();
        var index = Assert.IsType<ViewResult>(await postsController.Index());
        var indexed = Assert.IsAssignableFrom<IEnumerable<Post>>(index.Model).Single();
        Assert.Equal(postId, indexed.Id);
        Assert.Null(indexed.ImageData);
        Assert.Null(indexed.Content);
        Assert.Equal("image/png", indexed.ContentType);

        var blogPage = Assert.IsType<ViewResult>(await postsController.BlogPostIndex(blogId, 1));
        var pagedPosts = Assert.IsAssignableFrom<IPagedList<Post>>(blogPage.Model);
        Assert.Equal(1, pagedPosts.PageNumber);
        Assert.Null(pagedPosts.Single().ImageData);

        var search = Assert.IsType<ViewResult>(await postsController.SearchIndex(1, "body"));
        var searched = Assert.IsAssignableFrom<IPagedList<Post>>(search.Model);
        Assert.Equal("public-note", searched.Single().Slug);
        Assert.Null(searched.Single().Content);
        Assert.Null(searched.Single().ImageData);

        var tagged = Assert.IsType<ViewResult>(await postsController.TagIndex("shipped", 1));
        Assert.Null(Assert.IsAssignableFrom<IPagedList<Post>>(tagged.Model).Single().ImageData);

        var blogs = Assert.IsType<ViewResult>(await database.CreateBlogsController().Index());
        var blog = Assert.IsAssignableFrom<IEnumerable<Blog.Models.Blog>>(blogs.Model).Single();
        Assert.Equal(blogId, blog.Id);
        Assert.Null(blog.ImageData);
        Assert.Equal("image/png", blog.ContentType);

        var home = Assert.IsType<ViewResult>(await database.CreateHomeController().IndexAsync(1));
        var homeBlog = Assert.IsAssignableFrom<IPagedList<BlogCard>>(home.Model).Single();
        Assert.Null(homeBlog.Author.ImageData);
        Assert.Equal("Ann", homeBlog.Author.FirstName);
        Assert.Equal("Author", homeBlog.Author.LastName);
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

        public ApplicationDbContext Context => _context;

        public Task<(int PostId, int BlogId)> AddPostAsync(string slug, ReadyStatus status, string authorId, string tag = null, bool includeImage = true)
        {
            return AddAsync(slug, status, authorId, tag, includeImage ? SampleImage : null);
        }

        private async Task<(int PostId, int BlogId)> AddAsync(string slug, ReadyStatus status, string authorId, string tag, byte[] image)
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
                ImageData = image,
                ContentType = image == null ? null : "image/png"
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
                ImageData = image,
                ContentType = image == null ? null : "image/png"
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

            return (post.Id, blog.Id);
        }

        public MediaController CreateMediaController(string userId = null, string role = null)
        {
            var controller = new MediaController(_context, CreateUserManager());
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = Principal(userId, role)
                }
            };
            return controller;
        }

        public PostsController CreatePostsController(string userId = null, string role = null)
        {
            var controller = new PostsController(
                _context,
                new SlugService(_context),
                new StubImageService(),
                CreateUserManager(),
                new SearchService(_context));
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = Principal(userId, role)
                }
            };
            return controller;
        }

        public BlogsController CreateBlogsController()
        {
            var controller = new BlogsController(_context, new StubImageService(), CreateUserManager());
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = Principal(null, null) }
            };
            return controller;
        }

        public HomeController CreateHomeController()
        {
            var controller = new HomeController(
                NullLogger<HomeController>.Instance,
                new FakeEmailSender(),
                _context,
                new MailCooldown(_context));
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = Principal(null, null) }
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

        private static ClaimsPrincipal Principal(string userId, string role)
        {
            var claims = new List<Claim>();
            if (userId != null)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
            }

            if (role != null)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            return new ClaimsPrincipal(new ClaimsIdentity(claims, claims.Count == 0 ? null : "test"));
        }
    }

    private sealed class FakeEmailSender : IBlogEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage) => Task.CompletedTask;

        public Task SendContactEmailAsync(string email, string subject, string name, string htmlMessage) => Task.CompletedTask;
    }
}
