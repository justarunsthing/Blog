using System.Net;
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
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Blog.Tests;

public class CommentNotificationTests
{
    [Fact]
    public async Task Create_sends_one_admin_mail_for_a_burst_and_still_saves_every_comment()
    {
        await using var database = new CommentDatabase();
        var postId = await database.AddPostAsync();
        var sender = new RecordingSender();
        var first = database.CreateController(sender, "203.0.113.8");
        var second = database.CreateController(sender, "203.0.113.9");

        var firstResult = await first.Create(new Comment { PostId = postId, Body = "First note" });
        var secondResult = await second.Create(new Comment { PostId = postId, Body = "Second note" });

        Assert.IsType<RedirectToActionResult>(firstResult);
        Assert.IsType<RedirectToActionResult>(secondResult);
        Assert.Equal(1, sender.Sends);
        Assert.Equal("admin@example.com", sender.To);
        Assert.Contains("First note", sender.Html);
        Assert.Equal(2, await database.Context.Comments.CountAsync());
    }

    [Fact]
    public async Task Create_releases_the_cooldown_when_the_notification_fails()
    {
        await using var database = new CommentDatabase();
        var postId = await database.AddPostAsync();
        var sender = new RecordingSender { Throw = true };
        var controller = database.CreateController(sender, "203.0.113.8");

        var result = await controller.Create(new Comment { PostId = postId, Body = "A note" });

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(0, await database.Context.MailSends.CountAsync(send => send.Purpose == MailCooldown.Comment));
        Assert.Equal(1, await database.Context.Comments.CountAsync());
    }

    private sealed class CommentDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public CommentDatabase()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            Context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options);
            Context.Database.EnsureCreated();
        }

        public ApplicationDbContext Context { get; }

        public async Task<int> AddPostAsync()
        {
            var author = new BlogUser
            {
                Id = "reader",
                UserName = "reader@example.com",
                NormalizedUserName = "READER@EXAMPLE.COM",
                Email = "reader@example.com",
                NormalizedEmail = "READER@EXAMPLE.COM",
                FirstName = "Ada",
                LastName = "Reader",
                SecurityStamp = Guid.NewGuid().ToString()
            };
            Context.Users.Add(author);
            var blog = new Blog.Models.Blog
            {
                Name = "Notes",
                Description = "Notes on the work",
                Created = DateTime.UtcNow,
                AuthorId = author.Id
            };
            Context.Blogs.Add(blog);
            await Context.SaveChangesAsync();
            var post = new Post
            {
                BlogId = blog.Id,
                AuthorId = author.Id,
                Title = "A public note",
                Abstract = "Short",
                Content = "Body",
                Created = DateTime.UtcNow,
                Status = ReadyStatus.Production,
                Slug = "a-public-note"
            };
            Context.Posts.Add(post);
            await Context.SaveChangesAsync();
            return post.Id;
        }

        public CommentsController CreateController(RecordingSender sender, string remoteIp)
        {
            var users = new UserManager<BlogUser>(
                new UserStore<BlogUser>(Context),
                Options.Create(new IdentityOptions()),
                new PasswordHasher<BlogUser>(),
                new List<IUserValidator<BlogUser>> { new UserValidator<BlogUser>() },
                new List<IPasswordValidator<BlogUser>> { new PasswordValidator<BlogUser>() },
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                null,
                NullLogger<UserManager<BlogUser>>.Instance);
            var http = new DefaultHttpContext();
            http.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
            http.Request.Scheme = "https";
            http.Request.Host = new HostString("example.com");
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, "reader") },
                "test"));
            var controller = new CommentsController(
                Context,
                users,
                sender,
                new MailCooldown(Context),
                Options.Create(new MailSettings { Mail = "admin@example.com", DisplayName = "Blog" }),
                NullLogger<CommentsController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = http },
                TempData = new TempDataDictionary(http, new DictionaryTempDataProvider())
            };
            return controller;
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class RecordingSender : IBlogEmailSender
    {
        public bool Throw { get; set; }
        public int Sends { get; private set; }
        public string To { get; private set; }
        public string Html { get; private set; }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (Throw)
            {
                throw new InvalidOperationException("Brevo refused the message.");
            }

            Sends++;
            To = email;
            Html = htmlMessage;
            return Task.CompletedTask;
        }

        public Task SendContactEmailAsync(string email, string subject, string name, string htmlMessage)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class DictionaryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
