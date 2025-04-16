using Blog.Data;
using Blog.Enums;
using Blog.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Blog.Services
{
    public class DataService(ApplicationDbContext dbContext, RoleManager<IdentityRole> roleManager, UserManager<BlogUser> userManager)
    {
        private readonly ApplicationDbContext _dbContext = dbContext;
        private readonly RoleManager<IdentityRole> _roleManager = roleManager;
        private readonly UserManager<BlogUser> _userManager = userManager;

        public async Task ManageDataAync()
        {
            // Create DB from migrations
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

            var adminUser = new BlogUser
            {
                Email = "justarunsthing@outlook.com",
                UserName = "justarunsthing@outlook.com",
                FirstName = "Arun",
                LastName = "Pun",
                EmailConfirmed = true
            };

            await _userManager.CreateAsync(adminUser, "Password1?");
            await _userManager.AddToRoleAsync(adminUser, BlogRole.Administrator.ToString());
            await _userManager.AddToRoleAsync(adminUser, BlogRole.Moderator.ToString());
        }
    }
}
