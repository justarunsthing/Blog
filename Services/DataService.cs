using Blog.Data;
using Blog.Enums;
using Blog.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Blog.Services
{
    public class DataService(ApplicationDbContext dbContext, RoleManager<IdentityRole> roleManager, UserManager<BlogUser> userManager, IConfiguration configuration)
    {
        private readonly ApplicationDbContext _dbContext = dbContext;
        private readonly RoleManager<IdentityRole> _roleManager = roleManager;
        private readonly UserManager<BlogUser> _userManager = userManager;
        private readonly IConfiguration _configuration = configuration;

        public async Task ManageDataAync()
        {
            await _dbContext.Database.MigrateAsync();
            await SeedRolesAsync();
            await SeedUsersAsync();
        }

        private async Task SeedRolesAsync()
        {
            if (_dbContext.Roles.Any())
                return;

            foreach (var role in Enum.GetNames<BlogRole>())
            {
                await _roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        private async Task SeedUsersAsync()
        {
            if (_dbContext.Users.Any())
                return;

            var email = _configuration["Seed:Email"];
            var password = _configuration["Seed:Password"];

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException("Set Seed:Email and Seed:Password before the first boot.");
            }

            var adminUser = new BlogUser
            {
                Email = email,
                UserName = email,
                FirstName = "Arun",
                LastName = "Pun",
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(adminUser, password);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));
            }

            await _userManager.AddToRoleAsync(adminUser, BlogRole.Administrator.ToString());
            await _userManager.AddToRoleAsync(adminUser, BlogRole.Moderator.ToString());
        }
    }
}
