using System.Security.Claims;
using Blog.Controllers;
using Blog.Data;
using Blog.Interfaces;
using Blog.Models;
using Blog.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Blog.Areas.Identity.Pages.Account.Manage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Blog.Tests;

public class UsersAdminTests
{
    [Fact]
    public async Task Disable_TurnsOffAReaderAndLeavesAnAdministratorActive()
    {
        await using var database = new UserDatabase();
        var users = CreateUserManager(database.Context);
        var roles = CreateRoleManager(database.Context);
        await roles.CreateAsync(new IdentityRole("Administrator"));

        var admin = await AddUserAsync(users, "admin@example.com");
        var otherAdmin = await AddUserAsync(users, "other-admin@example.com");
        var reader = await AddUserAsync(users, "reader@example.com");
        await users.AddToRoleAsync(admin, "Administrator");
        await users.AddToRoleAsync(otherAdmin, "Administrator");
        var stamp = reader.SecurityStamp;

        var controller = CreateController(users, admin.Id);

        var ownAccount = await controller.Disable(admin.Id);
        Assert.Equal("You cannot disable your own account.", controller.TempData["UserMessage"]);
        Assert.IsType<RedirectToActionResult>(ownAccount);

        var blocked = await controller.Disable(otherAdmin.Id);
        Assert.Equal("An administrator account stays active.", controller.TempData["UserMessage"]);
        Assert.False((await users.FindByIdAsync(otherAdmin.Id)).Disabled);

        var disabled = await controller.Disable(reader.Id);
        var storedReader = await users.FindByIdAsync(reader.Id);
        var storedAdmin = await users.FindByIdAsync(admin.Id);

        Assert.IsType<RedirectToActionResult>(blocked);
        Assert.IsType<RedirectToActionResult>(disabled);
        Assert.False(storedAdmin.Disabled);
        Assert.True(storedReader.Disabled);
        Assert.NotEqual(stamp, storedReader.SecurityStamp);
        Assert.Equal("Disabled reader@example.com.", controller.TempData["UserMessage"]);
    }

    [Fact]
    public async Task IsOnlyAdministrator_IsTrueOnlyForTheSoleAdministrator()
    {
        await using var database = new UserDatabase();
        var users = CreateUserManager(database.Context);
        var roles = CreateRoleManager(database.Context);
        await roles.CreateAsync(new IdentityRole("Administrator"));

        var admin = await AddUserAsync(users, "admin@example.com");
        var reader = await AddUserAsync(users, "reader@example.com");
        await users.AddToRoleAsync(admin, "Administrator");

        Assert.True(await AdministratorGuard.IsOnlyAdministratorAsync(users, admin));
        Assert.False(await AdministratorGuard.IsOnlyAdministratorAsync(users, reader));

        var otherAdmin = await AddUserAsync(users, "other-admin@example.com");
        await users.AddToRoleAsync(otherAdmin, "Administrator");

        Assert.False(await AdministratorGuard.IsOnlyAdministratorAsync(users, admin));
        Assert.False(await AdministratorGuard.IsOnlyAdministratorAsync(users, otherAdmin));
    }

    [Fact]
    public async Task DeletePersonalData_RefusesTheOnlyAdministrator()
    {
        await using var database = new UserDatabase();
        var users = CreateUserManager(database.Context);
        var roles = CreateRoleManager(database.Context);
        await roles.CreateAsync(new IdentityRole("Administrator"));
        var admin = await AddUserAsync(users, "admin@example.com");
        await users.AddToRoleAsync(admin, "Administrator");

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, admin.Id)
            }, "test"))
        };
        var page = new DeletePersonalDataModel(users, null, NullLogger<DeletePersonalDataModel>.Instance)
        {
            PageContext = new PageContext { HttpContext = http }
        };

        var result = await page.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(page.IsOnlyAdministrator);
        Assert.NotNull(await users.FindByIdAsync(admin.Id));
    }

    [Fact]
    public async Task Enable_ClearsTheDisabledFlag()
    {
        await using var database = new UserDatabase();
        var users = CreateUserManager(database.Context);
        var reader = await AddUserAsync(users, "reader@example.com");
        reader.Disabled = true;
        await users.UpdateAsync(reader);
        var controller = CreateController(users, "someone-else");

        var result = await controller.Enable(reader.Id);
        var stored = await users.FindByIdAsync(reader.Id);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.False(stored.Disabled);
        Assert.Equal("Enabled reader@example.com.", controller.TempData["UserMessage"]);
    }

    private static UsersController CreateController(UserManager<BlogUser> users, string currentUserId)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, currentUserId)
            }, "test"))
        };
        var controller = new UsersController(users, new UnusedSender())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new DictionaryTempDataProvider())
        };
        return controller;
    }

    private static async Task<BlogUser> AddUserAsync(UserManager<BlogUser> users, string email)
    {
        var user = new BlogUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = "Ada",
            LastName = "Lovelace"
        };
        var created = await users.CreateAsync(user, "Password1?");
        Assert.True(created.Succeeded);
        return user;
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

    private static RoleManager<IdentityRole> CreateRoleManager(ApplicationDbContext context)
    {
        return new RoleManager<IdentityRole>(
            new RoleStore<IdentityRole>(context),
            new List<IRoleValidator<IdentityRole>> { new RoleValidator<IdentityRole>() },
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<IdentityRole>>.Instance);
    }

    private sealed class UserDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public UserDatabase()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
            Context = new ApplicationDbContext(options);
            Context.Database.EnsureCreated();
        }

        public ApplicationDbContext Context { get; }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class UnusedSender : IBlogEmailSender
    {
        public Task SendContactEmailAsync(string email, string subject, string name, string htmlMessage) => Task.CompletedTask;

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
