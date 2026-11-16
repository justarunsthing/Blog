using System.Security.Claims;
using System.Xml.Linq;
using Blog.Controllers;
using Blog.Data;
using Blog.Enums;
using Blog.Models;
using Blog.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Blog.Tests;

public class DiscoveryMetadataTests
{
    [Fact]
    public async Task Feed_includes_production_posts_and_skips_preview_and_incomplete()
    {
        await using var database = new TestDatabase();
        var published = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var created = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        await database.AddPostAsync("ship-note", ReadyStatus.Production, "Ship & tell", "A & B <note>", published, created);
        await database.AddPostAsync("newer-note", ReadyStatus.Production, "Newer note", "Fell back to created", null, created);
        await database.AddPostAsync("preview-note", ReadyStatus.Preview, "Soon", "Not yet", published, created);
        await database.AddPostAsync("draft-note", ReadyStatus.Incomplete, "Draft", "Hidden", published, created);

        var result = await database.CreateDiscovery().Feed();

        var content = Assert.IsType<ContentResult>(result);
        Assert.Contains("application/rss+xml", content.ContentType, StringComparison.OrdinalIgnoreCase);
        var document = XDocument.Parse(content.Content);
        var channel = document.Root.Element("channel");
        Assert.Equal("justarunsthing's Tech Blog", channel.Element("title").Value);
        Assert.Equal("https://example.com/", channel.Element("link").Value);
        Assert.Equal("justarunsthing's Tech Blog", channel.Element("description").Value);

        var items = channel.Elements("item").ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal("https://example.com/BlogPosts/UrlFriendly/newer-note", items[0].Element("link").Value);
        Assert.Equal("Mon, 01 Jun 2026 08:00:00 GMT", items[0].Element("pubDate").Value);
        Assert.Equal("https://example.com/BlogPosts/UrlFriendly/ship-note", items[1].Element("link").Value);
        Assert.Equal("Ship & tell", items[1].Element("title").Value);
        Assert.Equal("A & B <note>", items[1].Element("description").Value);
        Assert.Equal("Fri, 02 Jan 2026 03:04:05 GMT", items[1].Element("pubDate").Value);
        Assert.DoesNotContain("preview-note", content.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("draft-note", content.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sitemap_includes_production_slugs_and_skips_preview()
    {
        await using var database = new TestDatabase();
        var published = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var production = await database.AddPostAsync("ship-note", ReadyStatus.Production, "Ship", "A public note", published, published);
        var preview = await database.AddPostAsync("preview-note", ReadyStatus.Preview, "Soon", "Not yet", published, published);
        await database.AddPostAsync("draft-note", ReadyStatus.Incomplete, "Draft", "Hidden", published, published);

        var result = await database.CreateDiscovery().Sitemap();

        var content = Assert.IsType<ContentResult>(result);
        Assert.StartsWith("application/xml", content.ContentType, StringComparison.OrdinalIgnoreCase);
        var document = XDocument.Parse(content.Content);
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var locs = document.Descendants(ns + "loc").Select(node => node.Value).ToList();
        Assert.Contains("https://example.com/", locs);
        Assert.Contains($"https://example.com/Posts/BlogPostIndex/{production.BlogId}", locs);
        Assert.Contains("https://example.com/BlogPosts/UrlFriendly/ship-note", locs);
        Assert.DoesNotContain(locs, loc => loc.Contains("preview-note", StringComparison.Ordinal));
        Assert.DoesNotContain(locs, loc => loc.Contains("draft-note", StringComparison.Ordinal));
        Assert.DoesNotContain(locs, loc => loc.Contains($"/Posts/BlogPostIndex/{preview.BlogId}", StringComparison.Ordinal));
        var postUrl = document.Descendants(ns + "url")
            .Single(url => url.Element(ns + "loc").Value.EndsWith("/ship-note", StringComparison.Ordinal));
        Assert.Equal("2026-01-02", postUrl.Element(ns + "lastmod").Value);
    }

    [Fact]
    public void Robots_points_crawlers_at_the_request_host_sitemap_without_a_database()
    {
        var content = Assert.IsType<ContentResult>(RobotsFor("https", "example.com"));
        Assert.Contains("text/plain", content.ContentType, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("User-agent: *", content.Content, StringComparison.Ordinal);
        Assert.Contains("Allow: /", content.Content, StringComparison.Ordinal);
        Assert.Contains("Sitemap: https://example.com/sitemap.xml", content.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Disallow", content.Content, StringComparison.OrdinalIgnoreCase);

        var otherHost = Assert.IsType<ContentResult>(RobotsFor("http", "blog.internal:8080"));
        Assert.Contains("Sitemap: http://blog.internal:8080/sitemap.xml", otherHost.Content, StringComparison.Ordinal);
    }

    private static IActionResult RobotsFor(string scheme, string host)
    {
        var controller = new DiscoveryController(null);
        var http = new DefaultHttpContext();
        http.Request.Scheme = scheme;
        http.Request.Host = new HostString(host);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = http
        };
        return controller.Robots();
    }

    [Fact]
    public async Task Details_sets_canonical_and_open_graph_for_production_only()
    {
        await using var database = new TestDatabase();
        var published = new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc);
        var covered = await database.AddPostAsync(
            "ship-note",
            ReadyStatus.Production,
            "Ship note",
            "A short abstract",
            published,
            published,
            "image/png");
        await database.AddPostAsync(
            "plain-note",
            ReadyStatus.Production,
            "Plain note",
            "No cover",
            published,
            published,
            contentType: null);
        await database.AddPostAsync(
            "preview-note",
            ReadyStatus.Preview,
            "Soon",
            "Not public yet",
            published,
            published,
            "image/png");

        var production = Assert.IsType<ViewResult>(await database.CreatePostsController().Details("ship-note"));
        Assert.Equal("https://example.com/BlogPosts/UrlFriendly/ship-note", production.ViewData["CanonicalUrl"]);
        Assert.Equal("https://example.com/BlogPosts/UrlFriendly/ship-note", production.ViewData["OgUrl"]);
        Assert.Equal("Ship note", production.ViewData["OgTitle"]);
        Assert.Equal("A short abstract", production.ViewData["OgDescription"]);
        Assert.Equal("A short abstract", production.ViewData["MetaDescription"]);
        Assert.Equal("article", production.ViewData["OgType"]);
        Assert.Equal($"https://example.com/media/posts/{covered.PostId}", production.ViewData["OgImage"]);
        Assert.Null(production.ViewData["Robots"]);

        var plain = Assert.IsType<ViewResult>(await database.CreatePostsController().Details("plain-note"));
        Assert.Equal("https://example.com/BlogPosts/UrlFriendly/plain-note", plain.ViewData["CanonicalUrl"]);
        Assert.Equal("article", plain.ViewData["OgType"]);
        Assert.Null(plain.ViewData["OgImage"]);

        var preview = Assert.IsType<ViewResult>(await database.CreatePostsController().Details("preview-note"));
        Assert.Null(preview.ViewData["CanonicalUrl"]);
        Assert.Null(preview.ViewData["OgTitle"]);
        Assert.Null(preview.ViewData["OgDescription"]);
        Assert.Null(preview.ViewData["OgUrl"]);
        Assert.Null(preview.ViewData["OgType"]);
        Assert.Null(preview.ViewData["OgImage"]);
        Assert.Null(preview.ViewData["MetaDescription"]);
        Assert.Equal("noindex", preview.ViewData["Robots"]);
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

        public async Task<(int PostId, int BlogId)> AddPostAsync(
            string slug,
            ReadyStatus status,
            string title,
            string abstractText,
            DateTime? published,
            DateTime created,
            string contentType = "image/png")
        {
            const string authorId = "author";
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
                Name = "Notes " + slug,
                Description = "Notes on the work",
                Created = created,
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
                Title = title,
                Abstract = abstractText,
                Content = "The post body",
                Created = created,
                Published = published,
                Status = status,
                Slug = slug,
                ImageData = contentType == null ? null : new byte[] { 1 },
                ContentType = contentType
            };
            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            return (post.Id, blog.Id);
        }

        public DiscoveryController CreateDiscovery()
        {
            var controller = new DiscoveryController(_context);
            AttachRequest(controller);
            return controller;
        }

        public PostsController CreatePostsController()
        {
            var controller = new PostsController(
                _context,
                new SlugService(_context),
                new StubImageService(),
                CreateUserManager(),
                new SearchService(_context));
            AttachRequest(controller);
            return controller;
        }

        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private static void AttachRequest(Controller controller)
        {
            var http = new DefaultHttpContext();
            http.Request.Scheme = "https";
            http.Request.Host = new HostString("example.com");
            http.User = new ClaimsPrincipal(new ClaimsIdentity());
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = http
            };
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
