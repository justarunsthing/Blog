using Blog.Controllers;
using Blog.Interfaces;
using Blog.Services;
using Blog.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Blog.Tests;

public class ContactRateLimitTests
{
    [Fact]
    public void TryBeginSend_AllowsOneAddressAgainAfterTheWindow()
    {
        var limit = new ContactRateLimit();
        var start = DateTimeOffset.UtcNow;

        Assert.True(limit.TryBeginSend("Ada@Example.com", start));
        Assert.False(limit.TryBeginSend("ada@example.com", start.AddMinutes(9)));
        Assert.True(limit.TryBeginSend(" ada@example.com ", start.Add(ContactRateLimit.Window)));
    }

    [Fact]
    public async Task Contact_DoesNotSendASecondMessageFromTheSameAddress()
    {
        var sender = new RecordingSender();
        var controller = new HomeController(
            NullLogger<HomeController>.Instance,
            sender,
            null,
            new ContactRateLimit());
        var first = new ContactMe { Name = "Ada", Email = "Ada@Example.com", Subject = "Hello", Message = "A note" };
        var second = new ContactMe { Name = "Ada", Email = "ada@example.com", Subject = "Hello", Message = "Another note" };

        var firstResult = await controller.Contact(first);
        var secondResult = await controller.Contact(second);

        Assert.Equal(1, sender.ContactSends);
        Assert.IsType<RedirectToActionResult>(firstResult);
        Assert.IsType<ViewResult>(secondResult);
        Assert.False(controller.ModelState.IsValid);
    }

    private sealed class RecordingSender : IBlogEmailSender
    {
        public int ContactSends { get; private set; }

        public Task SendContactEmailAsync(string email, string subject, string name, string htmlMessage)
        {
            ContactSends++;
            return Task.CompletedTask;
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage) => Task.CompletedTask;
    }
}
