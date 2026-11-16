using Blog.Enums;
using Blog.Models;
using Microsoft.AspNetCore.Identity;

namespace Blog.Services
{
    public static class AdministratorGuard
    {
        public static async Task<bool> IsOnlyAdministratorAsync(UserManager<BlogUser> users, BlogUser user)
        {
            if (user == null || !await users.IsInRoleAsync(user, BlogRole.Administrator.ToString()))
            {
                return false;
            }

            var administrators = await users.GetUsersInRoleAsync(BlogRole.Administrator.ToString());
            return administrators.Count <= 1;
        }
    }
}
