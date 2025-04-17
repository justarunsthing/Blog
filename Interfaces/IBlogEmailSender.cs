using Microsoft.AspNetCore.Identity.UI.Services;

namespace Blog.Interfaces
{
    public interface IBlogEmailSender : IEmailSender
    {
        Task SendContactEmailAsync(string email, string subject, string name, string htmlMessage);
    }
}
