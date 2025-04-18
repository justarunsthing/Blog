using MimeKit;
using Blog.Interfaces;
using Blog.ViewModels;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MailKit.Security;

namespace Blog.Services
{
    public class EmailService(IOptions<MailSettings> mailSettings) : IBlogEmailSender
    {
        private readonly MailSettings _mailSettings = mailSettings.Value;

        public async Task SendContactEmailAsync(string emailFrom, string subject, string name, string htmlMessage)
        {
            var email = new MimeMessage
            {
                Sender = MailboxAddress.Parse(_mailSettings.Mail),
                Subject = subject
            };

            email.To.Add(MailboxAddress.Parse(_mailSettings.Mail));

            var builder = new BodyBuilder
            {
                HtmlBody = $"<b>{name}</b> has sent you an email and can be reached at: <b>{emailFrom}</b><br/><br/>{htmlMessage}"
            };

            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            smtp.Connect(_mailSettings.Host, _mailSettings.Port, SecureSocketOptions.StartTls);
            smtp.Authenticate(_mailSettings.Mail, _mailSettings.Password);

            await smtp.SendAsync(email);

            smtp.Disconnect(true);
        }
        
        public async Task SendEmailAsync(string emailTo, string subject, string htmlMessage)
        {
            var email = new MimeMessage()
            {
                Sender = MailboxAddress.Parse(_mailSettings.Mail),
                Subject = subject
            };

            email.To.Add(MailboxAddress.Parse(emailTo));

            var builder = new BodyBuilder
            {
                HtmlBody = htmlMessage
            };

            email.Body = builder.ToMessageBody();

            // use smtp client to send email
            using var smtp = new SmtpClient();
            smtp.Connect(_mailSettings.Host, _mailSettings.Port, SecureSocketOptions.StartTls);
            smtp.Authenticate(_mailSettings.Mail, _mailSettings.Password);

            await smtp.SendAsync(email);

            smtp.Disconnect(true);
        }
    }
}
