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
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Blog.Tests;

public class PostEditTests
{
    [Fact]
    public async Task Edit_copies_BlogId_onto_the_saved_post()
    {
        await using var database = new PostDatabase();
        var notes = await database.AddBlogAsync("Notes");
        var essays = await database.AddBlogAsync("Essays");
        var post = await database.AddPostAsync(notes.Id, "notes-post", "notes-post", ReadyStatus.Production);

        var result = await database.CreateController(PostDatabase.AuthorId).Edit(
            post.Id,
            Form(post, essays.Id, "notes-post"),
            null,
            new List<string>());

        Assert.IsType<RedirectToActionResult>(result);
        var saved = await database.Context.Posts.AsNoTracking().SingleAsync(item => item.Id == post.Id);
        Assert.Equal(essays.Id, saved.BlogId);
    }

    [Fact]
    public async Task Edit_returns_the_form_when_the_blog_does_not_exist()
    {
        await using var database = new PostDatabase();
        var notes = await database.AddBlogAsync("Notes");
        var post = await database.AddPostAsync(notes.Id, "notes-post", "notes-post", ReadyStatus.Production);
        var controller = database.CreateController(PostDatabase.AuthorId);

        var result = await controller.Edit(post.Id, Form(post, 424242, "notes-post"), null, new List<string>());

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        var saved = await database.Context.Posts.AsNoTracking().SingleAsync(item => item.Id == post.Id);
        Assert.Equal(notes.Id, saved.BlogId);
        AssertBlogListUsesNames(result, notes.Name);
    }

    [Fact]
    public async Task Create_rejects_a_tag_longer_than_25_characters()
    {
        await using var database = new PostDatabase();
        var notes = await database.AddBlogAsync("Notes");
        var controller = database.CreateController(PostDatabase.AuthorId);
        var tooLong = new string('a', 26);
        var post = new Post
        {
            BlogId = notes.Id,
            Title = "A valid title",
            Abstract = "A short abstract",
            Content = "The post body",
            Status = ReadyStatus.Production
        };

        var result = await controller.Create(post, new List<string> { tooLong });

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(tooLong, view.ViewData["TagValues"]);
        Assert.Equal(0, await database.Context.Tags.CountAsync());
        Assert.Equal(0, await database.Context.Posts.CountAsync());
        AssertBlogListUsesNames(result, notes.Name, notes.Id);
    }

    [Fact]
    public async Task Edit_rejects_a_tag_longer_than_25_characters()
    {
        await using var database = new PostDatabase();
        var notes = await database.AddBlogAsync("Notes");
        var post = await database.AddPostAsync(notes.Id, "notes-post", "notes-post", ReadyStatus.Production, "kept");
        var controller = database.CreateController(PostDatabase.AuthorId);
        var tooLong = new string('b', 26);

        var result = await controller.Edit(post.Id, Form(post, notes.Id, "notes-post"), null, new List<string> { tooLong });

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(tooLong, view.ViewData["TagValues"]);
        Assert.Equal(new[] { "kept" }, await database.Context.Tags.Select(tag => tag.Text).ToListAsync());
        AssertBlogListUsesNames(result, notes.Name, notes.Id);
    }

    [Fact]
    public async Task Create_keeps_blog_names_when_the_slug_is_already_taken()
    {
        await using var database = new PostDatabase();
        var notes = await database.AddBlogAsync("Notes");
        await database.AddPostAsync(notes.Id, "Shared Title", "shared-title", ReadyStatus.Production);
        var controller = database.CreateController(PostDatabase.AuthorId);
        var post = new Post
        {
            BlogId = notes.Id,
            Title = "Shared Title",
            Abstract = "A short abstract",
            Content = "The post body",
            Status = ReadyStatus.Production
        };

        var result = await controller.Create(post, new List<string> { "draft" });

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(1, await database.Context.Posts.CountAsync());
        Assert.Equal(0, await database.Context.Tags.CountAsync());
        AssertBlogListUsesNames(result, notes.Name, notes.Id);
    }

    [Fact]
    public async Task Details_redirects_an_old_slug_to_the_current_slug()
    {
        await using var database = new PostDatabase();
        var notes = await database.AddBlogAsync("Notes");
        var post = await database.AddPostAsync(notes.Id, "Old Title", "old-title", ReadyStatus.Production);
        var editor = database.CreateController(PostDatabase.AuthorId);

        var edited = await editor.Edit(post.Id, Form(post, notes.Id, "New Title"), null, new List<string>());

        Assert.IsType<RedirectToActionResult>(edited);
        var oldSlug = await database.CreateController().Details("old-title");
        var redirect = Assert.IsType<RedirectResult>(oldSlug);
        Assert.True(redirect.Permanent);
        Assert.Equal("/BlogPosts/UrlFriendly/new-title", redirect.Url);

        var current = await database.CreateController().Details("new-title");
        var view = Assert.IsType<ViewResult>(current);
        var model = Assert.IsType<PostDetailViewModel>(view.Model);
        Assert.Equal("new-title", model.Post.Slug);
    }

    [Fact]
    public async Task Details_hides_an_old_slug_when_the_post_is_not_readable()
    {
        await using var database = new PostDatabase();
        var notes = await database.AddBlogAsync("Notes");
        var post = await database.AddPostAsync(notes.Id, "Draft One", "draft-one", ReadyStatus.Incomplete);
        var editor = database.CreateController(PostDatabase.AuthorId);

        var edited = await editor.Edit(post.Id, Form(post, notes.Id, "Draft Two"), null, new List<string>());

        Assert.IsType<RedirectToActionResult>(edited);
        Assert.IsType<NotFoundResult>(await database.CreateController().Details("draft-one"));
        Assert.IsType<NotFoundResult>(await database.CreateController().Details("draft-two"));
    }

    [Fact]
    public async Task Create_sets_Published_when_the_post_is_production()
    {
        await using var database = new PostDatabase();
        var notes = await database.AddBlogAsync("Notes");
        var controller = database.CreateController(PostDatabase.AuthorId);
        var before = DateTime.UtcNow;
        var post = new Post
        {
            BlogId = notes.Id,
            Title = "A published note",
            Abstract = "A short abstract",
            Content = "The post body",
            Status = ReadyStatus.Production
        };

        var result = await controller.Create(post, new List<string>());

        Assert.IsType<RedirectToActionResult>(result);
        var saved = await database.Context.Posts.AsNoTracking().SingleAsync();
        Assert.NotNull(saved.Published);
        Assert.True(saved.Published.Value >= before.AddSeconds(-1));
    }

    [Fact]
    public async Task Edit_sets_Published_when_a_draft_becomes_production()
    {
        await using var database = new PostDatabase();
        var notes = await database.AddBlogAsync("Notes");
        var post = await database.AddPostAsync(notes.Id, "Draft note", "draft-note", ReadyStatus.Incomplete);
        var before = DateTime.UtcNow;
        var form = Form(post, notes.Id, post.Title);
        form.Status = ReadyStatus.Production;

        var result = await database.CreateController(PostDatabase.AuthorId).Edit(post.Id, form, null, new List<string>());

        Assert.IsType<RedirectToActionResult>(result);
        var saved = await database.Context.Posts.AsNoTracking().SingleAsync(item => item.Id == post.Id);
        Assert.Equal(ReadyStatus.Production, saved.Status);
        Assert.NotNull(saved.Published);
        Assert.True(saved.Published.Value >= before.AddSeconds(-1));
    }

    [Fact]
    public async Task Edit_keeps_Published_on_a_later_edit()
    {
        await using var database = new PostDatabase();
        var notes = await database.AddBlogAsync("Notes");
        var post = await database.AddPostAsync(notes.Id, "Published note", "published-note", ReadyStatus.Production);
        var published = new DateTime(2020, 5, 1, 8, 30, 0, DateTimeKind.Utc);
        post.Published = published;
        await database.Context.SaveChangesAsync();
        var form = Form(post, notes.Id, "Renamed note");
        form.Content = "A revised body";
        form.Status = ReadyStatus.Production;

        var result = await database.CreateController(PostDatabase.AuthorId).Edit(post.Id, form, null, new List<string>());

        Assert.IsType<RedirectToActionResult>(result);
        var saved = await database.Context.Posts.AsNoTracking().SingleAsync(item => item.Id == post.Id);
        Assert.Equal(published, DateTime.SpecifyKind(saved.Published.Value, DateTimeKind.Utc));

        form = Form(saved, notes.Id, "Renamed note");
        form.Status = ReadyStatus.Preview;
        Assert.IsType<RedirectToActionResult>(await database.CreateController(PostDatabase.AuthorId).Edit(post.Id, form, null, new List<string>()));
        saved = await database.Context.Posts.AsNoTracking().SingleAsync(item => item.Id == post.Id);
        Assert.Equal(ReadyStatus.Preview, saved.Status);
        Assert.Equal(published, DateTime.SpecifyKind(saved.Published.Value, DateTimeKind.Utc));

        form = Form(saved, notes.Id, "Renamed note");
        form.Status = ReadyStatus.Production;
        Assert.IsType<RedirectToActionResult>(await database.CreateController(PostDatabase.AuthorId).Edit(post.Id, form, null, new List<string>()));
        saved = await database.Context.Posts.AsNoTracking().SingleAsync(item => item.Id == post.Id);
        Assert.Equal(published, DateTime.SpecifyKind(saved.Published.Value, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Search_orders_by_Published_ahead_of_a_later_Created_post()
    {
        await using var database = new PostDatabase();
        var notes = await database.AddBlogAsync("Notes");
        var createdLater = await database.AddPostAsync(notes.Id, "Created later", "created-later", ReadyStatus.Production);
        var publishedLater = await database.AddPostAsync(notes.Id, "Published later", "published-later", ReadyStatus.Production);
        createdLater.Created = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        createdLater.Published = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        publishedLater.Created = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        publishedLater.Published = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        await database.Context.SaveChangesAsync();

        var titles = await new SearchService(database.Context).Search(null).Select(post => post.Title).ToListAsync();

        Assert.Equal(new[] { "Published later", "Created later" }, titles);
    }

    [Fact]
    public async Task Home_orders_blogs_by_the_newest_published_post()
    {
        await using var database = new PostDatabase();
        var olderBlog = await database.AddBlogAsync("Older blog");
        var newerBlog = await database.AddBlogAsync("Newer blog");
        var oldPost = await database.AddPostAsync(olderBlog.Id, "Old post", "old-post", ReadyStatus.Production);
        var newPost = await database.AddPostAsync(newerBlog.Id, "New post", "new-post", ReadyStatus.Production);
        olderBlog.Created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        newerBlog.Created = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        oldPost.Created = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        oldPost.Published = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        newPost.Created = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        newPost.Published = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        await database.Context.SaveChangesAsync();

        var controller = new HomeController(
            NullLogger<HomeController>.Instance,
            null,
            database.Context,
            new MailCooldown(database.Context));

        var result = await controller.IndexAsync(null);

        var view = Assert.IsType<ViewResult>(result);
        var blogs = Assert.IsAssignableFrom<IEnumerable<BlogCard>>(view.Model).ToList();
        Assert.Equal(new[] { "Newer blog", "Older blog" }, blogs.Select(blog => blog.Name).ToArray());
        Assert.Equal(new DateTime(2025, 6, 1), blogs[0].LatestPublished.Date);
    }

    [Fact]
    public async Task Edit_reclaims_a_previous_slug_without_duplicating_the_alias()
    {
        await using var database = new PostDatabase();
        var notes = await database.AddBlogAsync("Notes");
        var post = await database.AddPostAsync(notes.Id, "Old Title", "old-title", ReadyStatus.Production);
        var editor = database.CreateController(PostDatabase.AuthorId);

        Assert.IsType<RedirectToActionResult>(await editor.Edit(post.Id, Form(post, notes.Id, "New Title"), null, new List<string>()));
        Assert.IsType<RedirectToActionResult>(await editor.Edit(post.Id, Form(post, notes.Id, "Old Title"), null, new List<string>()));

        var current = Assert.IsType<ViewResult>(await database.CreateController().Details("old-title"));
        Assert.Equal("old-title", Assert.IsType<PostDetailViewModel>(current.Model).Post.Slug);
        var redirect = Assert.IsType<RedirectResult>(await database.CreateController().Details("new-title"));
        Assert.True(redirect.Permanent);
        Assert.Equal("/BlogPosts/UrlFriendly/old-title", redirect.Url);
        Assert.Equal(new[] { "new-title" }, await database.Context.PostAliases.Select(alias => alias.Slug).ToListAsync());
    }

    private static Post Form(Post post, int blogId, string title)
    {
        return new Post
        {
            Id = post.Id,
            BlogId = blogId,
            Title = title,
            Abstract = post.Abstract,
            Content = post.Content,
            Status = post.Status
        };
    }

    private static void AssertBlogListUsesNames(IActionResult result, string blogName, int? selectedBlogId = null)
    {
        var view = Assert.IsType<ViewResult>(result);
        var blogs = Assert.IsType<SelectList>(view.ViewData["BlogId"]);
        Assert.Equal("Name", blogs.DataTextField);
        Assert.Contains(blogs, item => item.Text == blogName);
        if (selectedBlogId != null)
        {
            Assert.Contains(blogs, item => item.Value == selectedBlogId.ToString() && item.Selected);
        }
    }

    private sealed class PostDatabase : IAsyncDisposable
    {
        public const string AuthorId = "author";
        private readonly SqliteConnection _connection;

        public PostDatabase()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
            Context = new ApplicationDbContext(options);
            Context.Database.EnsureCreated();
        }

        public ApplicationDbContext Context { get; }

        public async Task<Blog.Models.Blog> AddBlogAsync(string name)
        {
            if (!await Context.Users.AnyAsync(user => user.Id == AuthorId))
            {
                Context.Users.Add(new BlogUser
                {
                    Id = AuthorId,
                    UserName = AuthorId,
                    NormalizedUserName = AuthorId.ToUpperInvariant(),
                    Email = AuthorId + "@example.com",
                    NormalizedEmail = (AuthorId + "@example.com").ToUpperInvariant(),
                    FirstName = "Ann",
                    LastName = "Author",
                    SecurityStamp = Guid.NewGuid().ToString()
                });
            }

            var blog = new Blog.Models.Blog
            {
                Name = name,
                Description = name + " described",
                Created = DateTime.UtcNow,
                AuthorId = AuthorId,
                ImageData = new byte[] { 1 },
                ContentType = "image/png"
            };
            Context.Blogs.Add(blog);
            await Context.SaveChangesAsync();
            return blog;
        }

        public async Task<Post> AddPostAsync(int blogId, string title, string slug, ReadyStatus status, string tag = null)
        {
            var post = new Post
            {
                BlogId = blogId,
                AuthorId = AuthorId,
                Title = title,
                Abstract = "A short abstract",
                Content = "The post body",
                Created = DateTime.UtcNow,
                Status = status,
                Slug = slug,
                ImageData = new byte[] { 1 },
                ContentType = "image/png"
            };
            Context.Posts.Add(post);
            await Context.SaveChangesAsync();

            if (tag != null)
            {
                Context.Tags.Add(new Tag
                {
                    PostId = post.Id,
                    AuthorId = AuthorId,
                    Text = tag
                });
                await Context.SaveChangesAsync();
            }

            return post;
        }

        public PostsController CreateController(string userId = null, string role = null)
        {
            var controller = new PostsController(
                Context,
                new SlugService(Context),
                new StubImageService(),
                CreateUserManager(),
                new SearchService(Context));

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
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private UserManager<BlogUser> CreateUserManager()
        {
            return new UserManager<BlogUser>(
                new UserStore<BlogUser>(Context),
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
