using Blog.Interfaces;
using Blog.ViewModels;
using Microsoft.Extensions.Options;

namespace Blog.Services
{
    public class EmailService(IOptions<MailSettings> mailSettings) : IBlogEmailSender
    {
        private readonly MailSettings _mailSettings = mailSettings.Value;

        public Task SendContactEmailAsync(string email, string subject, string name, string htmlMessage)
        {
            throw new NotImplementedException();
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            throw new NotImplementedException();
        }
    }
}
