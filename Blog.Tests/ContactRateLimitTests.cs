using System.Net;
using Blog.Areas.Identity.Pages.Account;
using Blog.Controllers;
using Blog.Data;
using Blog.Interfaces;
using Blog.Models;
using Blog.Services;
using Blog.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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
        Assert.True(await cooldown.TryBeginAsync(MailCooldown.EmailConfirmation, "ada@example.com", "203.0.113.8", MailCooldown.AccountWindow, start));
        Assert.False(await cooldown.TryBeginAsync(MailCooldown.EmailConfirmation, "other@example.com", "203.0.113.8", MailCooldown.AccountWindow, start.AddMinutes(1)));
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

    [Fact]
    public async Task ResendConfirmation_WaitsTwoMinutesEvenForAnUnknownAddress()
    {
        await using var database = new MailDatabase();
        var sender = new RecordingSender();
        var users = CreateUserManager(database.Context);
        var cooldown = new MailCooldown(database.Context);

        var first = CreateResendPage(users, sender, cooldown, "203.0.113.8");
        first.Input = new ResendEmailConfirmationModel.InputModel { Email = "nobody@example.com" };
        var firstResult = await first.OnPostAsync();

        var second = CreateResendPage(users, sender, cooldown, "203.0.113.8");
        second.Input = new ResendEmailConfirmationModel.InputModel { Email = "other@example.com" };
        var secondResult = await second.OnPostAsync();

        Assert.IsType<PageResult>(firstResult);
        Assert.Contains("Verification email sent. Please check your email.", Errors(first));
        Assert.IsType<PageResult>(secondResult);
        Assert.Contains("Please wait 2 minutes before requesting another confirmation.", Errors(second));
        Assert.Equal(0, sender.ConfirmationSends);
        Assert.Equal(1, await database.Context.MailSends.CountAsync(send => send.Purpose == MailCooldown.EmailConfirmation));
    }

    private static ResendEmailConfirmationModel CreateResendPage(UserManager<BlogUser> users, RecordingSender sender, MailCooldown cooldown, string remoteIp)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        return new ResendEmailConfirmationModel(users, sender, cooldown)
        {
            PageContext = new PageContext { HttpContext = httpContext }
        };
    }

    private static UserManager<BlogUser> CreateUserManager(ApplicationDbContext context)
    {
        return new UserManager<BlogUser>(
            new UserStore<BlogUser>(context),
            Options.Create(new IdentityOptions()),
            new PasswordHasher<BlogUser>(),
            new List<IUserValidator<BlogUser>> { new UserValidator<BlogUser>() },
            new List<IPasswordValidator<BlogUser>> { new PasswordValidator<BlogUser>() },
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null,
            NullLogger<UserManager<BlogUser>>.Instance);
    }

    private static IEnumerable<string> Errors(PageModel page)
    {
        return page.ModelState.Values.SelectMany(entry => entry.Errors).Select(error => error.ErrorMessage);
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

        public int ConfirmationSends { get; private set; }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            ConfirmationSends++;
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
