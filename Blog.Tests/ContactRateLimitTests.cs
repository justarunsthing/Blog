using System.Net;
using Blog.Controllers;
using Blog.Data;
using Blog.Interfaces;
using Blog.Services;
using Blog.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Blog.Tests;

public class ContactRateLimitTests
{
    [Fact]
    public async Task TryBeginAsync_BlocksAChangedAddressFromTheSameClient()
    {
        await using var database = new MailDatabase();
        var start = DateTimeOffset.Parse("2026-11-13T08:00:00Z");
        var cooldown = new MailCooldown(database.Context);

        Assert.True(await cooldown.TryBeginAsync(MailCooldown.Contact, "ada@example.com", "203.0.113.8", MailCooldown.ContactWindow, start));
        Assert.False(await cooldown.TryBeginAsync(MailCooldown.Contact, "other@example.com", "203.0.113.8", MailCooldown.ContactWindow, start.AddMinutes(1)));
        Assert.True(await cooldown.TryBeginAsync(MailCooldown.Registration, "ada@example.com", "203.0.113.8", MailCooldown.AccountWindow, start));
        Assert.False(await cooldown.TryBeginAsync(MailCooldown.Registration, "other@example.com", "203.0.113.8", MailCooldown.AccountWindow, start.AddMinutes(1)));
        Assert.True(await cooldown.TryBeginAsync(MailCooldown.PasswordReset, "ada@example.com", "203.0.113.8", MailCooldown.AccountWindow, start));
        Assert.False(await cooldown.TryBeginAsync(MailCooldown.PasswordReset, "second@example.com", "203.0.113.8", MailCooldown.AccountWindow, start.AddMinutes(1)));
    }

    [Fact]
    public async Task TryBeginAsync_KeepsTheWaitAfterANewContextOpensTheSameDatabase()
    {
        await using var database = new MailDatabase();
        var start = DateTimeOffset.Parse("2026-11-13T08:00:00Z");
        Assert.True(await new MailCooldown(database.Context).TryBeginAsync(MailCooldown.Contact, "ada@example.com", "203.0.113.8", MailCooldown.ContactWindow, start));

        await using var restarted = new ApplicationDbContext(database.Options);
        var again = await new MailCooldown(restarted).TryBeginAsync(MailCooldown.Contact, "other@example.com", "203.0.113.8", MailCooldown.ContactWindow, start.AddMinutes(1));

        Assert.False(again);
    }

    [Fact]
    public async Task Contact_DoesNotSendASecondMessageFromTheSameAddress()
    {
        await using var database = new MailDatabase();
        var sender = new RecordingSender();
        var controller = new HomeController(
            NullLogger<HomeController>.Instance,
            sender,
            database.Context,
            new MailCooldown(database.Context));
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.8");
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new DictionaryTempDataProvider());
        var first = new ContactMe { Name = "Ada", Email = "Ada@Example.com", Subject = "Hello", Message = "A note" };
        var second = new ContactMe { Name = "Ada", Email = "other@example.com", Subject = "Hello", Message = "Another note" };

        var firstResult = await controller.Contact(first);
        var secondResult = await controller.Contact(second);

        Assert.Equal(1, sender.ContactSends);
        Assert.IsType<RedirectToActionResult>(firstResult);
        Assert.Equal("Message sent.", controller.TempData["SweetAlert"]);
        Assert.IsType<ViewResult>(secondResult);
        Assert.False(controller.ModelState.IsValid);
    }

    private sealed class MailDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public MailDatabase()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            Options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
            Context = new ApplicationDbContext(Options);
            Context.Database.EnsureCreated();
        }

        public ApplicationDbContext Context { get; }
        public DbContextOptions<ApplicationDbContext> Options { get; }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class RecordingSender : IBlogEmailSender
    {
        public int ContactSends { get; private set; }

        public Task SendContactEmailAsync(string email, string subject, string name, string htmlMessage)
        {
            ContactSends++;
            return Task.CompletedTask;
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage) => Task.CompletedTask;
    }

    private sealed class DictionaryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
