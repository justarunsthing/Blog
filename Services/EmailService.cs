using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Blog.Interfaces;
using Blog.ViewModels;
using Microsoft.Extensions.Options;

namespace Blog.Services
{
    public class EmailService(HttpClient httpClient, IOptions<MailSettings> mailSettings, IOptions<BrevoSettings> brevo) : IBlogEmailSender
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly MailSettings _mailSettings = mailSettings.Value;
        private readonly BrevoSettings _brevo = brevo.Value;

        public Task SendContactEmailAsync(string emailFrom, string subject, string name, string htmlMessage)
        {
            var html = $"<b>{name}</b> has sent you an email and can be reached at: <b>{emailFrom}</b><br/><br/>{htmlMessage}";
            return SendAsync(_mailSettings.Mail, subject, html, emailFrom);
        }

        public Task SendEmailAsync(string emailTo, string subject, string htmlMessage)
        {
            return SendAsync(emailTo, subject, htmlMessage, null);
        }

        private async Task SendAsync(string to, string subject, string html, string replyTo)
        {
            if (string.IsNullOrWhiteSpace(_brevo.ApiKey))
            {
                throw new InvalidOperationException("Set Brevo:ApiKey before sending mail.");
            }

            var message = new BrevoMessage
            {
                sender = new BrevoAddress(_mailSettings.Mail, _mailSettings.DisplayName),
                to = [new BrevoAddress(to)],
                replyTo = string.IsNullOrWhiteSpace(replyTo) ? null : new BrevoAddress(replyTo),
                subject = subject,
                htmlContent = html
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "v3/smtp/email");
            request.Headers.Add("api-key", _brevo.ApiKey);
            request.Content = JsonContent.Create(message);
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Brevo refused the message ({(int)response.StatusCode}). {body}");
            }
        }

        private sealed class BrevoMessage
        {
            public BrevoAddress sender { get; set; }
            public BrevoAddress[] to { get; set; }

            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public BrevoAddress replyTo { get; set; }
            public string subject { get; set; }
            public string htmlContent { get; set; }
        }

        private sealed record BrevoAddress(string email, string name = null);
    }
}
