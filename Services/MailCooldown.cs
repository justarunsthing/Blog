using System.Net;
using Blog.Data;
using Blog.Models;
using Microsoft.EntityFrameworkCore;

namespace Blog.Services
{
    public class MailCooldown(ApplicationDbContext context)
    {
        public const string Contact = "Contact";
        public const string Registration = "Registration";
        public const string PasswordReset = "PasswordReset";
        public const string EmailConfirmation = "EmailConfirmation";
        public static readonly TimeSpan ContactWindow = TimeSpan.FromMinutes(10);
        public static readonly TimeSpan AccountWindow = TimeSpan.FromMinutes(2);

        private readonly ApplicationDbContext _context = context;

        public async Task<bool> TryBeginAsync(string purpose, string email, string remoteIp, TimeSpan window, DateTimeOffset? utcNow = null)
        {
            var now = (utcNow ?? DateTimeOffset.UtcNow).UtcDateTime;
            var since = now - window;
            var normalizedEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
            var ip = NormalizeIp(remoteIp);

            await _context.MailSends.Where(send => send.Created < now - ContactWindow).ExecuteDeleteAsync();

            var recent = _context.MailSends.Where(send => send.Purpose == purpose && send.Created >= since);
            var blocked = await recent.AnyAsync(send => send.RemoteIp == ip || (normalizedEmail != null && send.Email == normalizedEmail));

            if (blocked)
            {
                return false;
            }

            _context.MailSends.Add(new MailSend
            {
                Purpose = purpose,
                Email = normalizedEmail,
                RemoteIp = ip,
                Created = now
            });
            await _context.SaveChangesAsync();

            return true;
        }

        public static string NormalizeIp(string remoteIp)
        {
            if (string.IsNullOrWhiteSpace(remoteIp))
            {
                return "unknown";
            }

            if (IPAddress.TryParse(remoteIp.Trim(), out var address) && address.IsIPv4MappedToIPv6)
            {
                return address.MapToIPv4().ToString();
            }

            return remoteIp.Trim();
        }
    }
}
