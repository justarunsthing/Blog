using System.Text;
using System.Text.Encodings.Web;
using Blog.Interfaces;
using Blog.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Blog.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class UsersController : Controller
    {
        private readonly UserManager<BlogUser> _userManager;
        private readonly IBlogEmailSender _emailSender;

        public UsersController(UserManager<BlogUser> userManager, IBlogEmailSender emailSender)
        {
            _userManager = userManager;
            _emailSender = emailSender;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.OrderBy(user => user.Email).ToListAsync();
            var rows = new List<UserRow>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                rows.Add(new UserRow
                {
                    User = user,
                    Roles = roles,
                    Locked = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow,
                    IsAdministrator = roles.Contains("Administrator")
                });
            }

            return View(rows);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);

            TempData["UserMessage"] = $"Unlocked {user.Email}.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Disable(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            if (user.Id == _userManager.GetUserId(User))
            {
                TempData["UserMessage"] = "You cannot disable your own account.";
                return RedirectToAction(nameof(Index));
            }

            if (await _userManager.IsInRoleAsync(user, "Administrator"))
            {
                TempData["UserMessage"] = "An administrator account stays active.";
                return RedirectToAction(nameof(Index));
            }

            if (user.Disabled)
            {
                TempData["UserMessage"] = $"{user.Email} is already disabled.";
                return RedirectToAction(nameof(Index));
            }

            user.Disabled = true;
            var disabled = await _userManager.UpdateAsync(user);
            if (!disabled.Succeeded)
            {
                TempData["UserMessage"] = $"Could not disable {user.Email}.";
                return RedirectToAction(nameof(Index));
            }

            await _userManager.UpdateSecurityStampAsync(user);

            TempData["UserMessage"] = $"Disabled {user.Email}.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Enable(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            if (!user.Disabled)
            {
                TempData["UserMessage"] = $"{user.Email} is already active.";
                return RedirectToAction(nameof(Index));
            }

            user.Disabled = false;
            var enabled = await _userManager.UpdateAsync(user);
            if (!enabled.Succeeded)
            {
                TempData["UserMessage"] = $"Could not enable {user.Email}.";
                return RedirectToAction(nameof(Index));
            }

            TempData["UserMessage"] = $"Enabled {user.Email}.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendPasswordReset(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new { area = "Identity", code },
                protocol: Request.Scheme);

            await _emailSender.SendEmailAsync(
                user.Email,
                "Reset Password",
                $"Please reset your password by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

            TempData["UserMessage"] = $"Password reset sent to {user.Email}.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendConfirmation(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            if (user.EmailConfirmed)
            {
                TempData["UserMessage"] = $"{user.Email} is already confirmed.";
                return RedirectToAction(nameof(Index));
            }

            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId = user.Id, code },
                protocol: Request.Scheme);

            await _emailSender.SendEmailAsync(
                user.Email,
                "Confirm your email",
                $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

            TempData["UserMessage"] = $"Confirmation sent to {user.Email}.";

            return RedirectToAction(nameof(Index));
        }

        public class UserRow
        {
            public BlogUser User { get; set; }
            public IList<string> Roles { get; set; }
            public bool Locked { get; set; }
            public bool IsAdministrator { get; set; }
        }
    }
}
