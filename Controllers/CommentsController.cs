using System.Net;
using Blog.Data;
using Blog.Enums;
using Blog.Interfaces;
using Blog.Models;
using Blog.Services;
using Blog.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Blog.Controllers
{
    public class CommentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<BlogUser> _userManager;
        private readonly IBlogEmailSender _emailSender;
        private readonly MailCooldown _mailCooldown;
        private readonly MailSettings _mailSettings;
        private readonly ILogger<CommentsController> _logger;

        public CommentsController(
            ApplicationDbContext context,
            UserManager<BlogUser> userManager,
            IBlogEmailSender emailSender,
            MailCooldown mailCooldown,
            IOptions<MailSettings> mailSettings,
            ILogger<CommentsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _emailSender = emailSender;
            _mailCooldown = mailCooldown;
            _mailSettings = mailSettings.Value;
            _logger = logger;
        }

        // GET: Comments
        [Authorize(Roles = "Moderator")]
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Comments.Include(c => c.Author).Include(c => c.Moderator).Include(c => c.Post);

            return View(await applicationDbContext.ToListAsync());
        }

        // POST: Comments/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("PostId,Body")] Comment comment)
        {
            var post = await _context.Posts.FindAsync(comment.PostId);

            if (post == null || !CanRead(post))
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                TempData["CommentError"] = string.Join(" ", ModelState.Values
                    .SelectMany(entry => entry.Errors)
                    .Select(error => error.ErrorMessage)
                    .Where(message => !string.IsNullOrWhiteSpace(message)));
                TempData["CommentBody"] = comment.Body;

                return RedirectToAction("Details", "Posts", new { slug = post.Slug }, "comment-section");
            }

            comment.AuthorId = _userManager.GetUserId(User);
            comment.Created = DateTime.UtcNow;

            _context.Add(comment);

            await _context.SaveChangesAsync();
            await NotifyAdminAsync(post, comment);

            return RedirectToAction("Details", "Posts", new { slug = post.Slug }, "comment-section");
        }

        // GET: Comments/Edit/5
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var comment = await _context.Comments.FindAsync(id);

            if (comment == null)
            {
                return NotFound();
            }

            if (!CanEdit(comment))
            {
                return Forbid();
            }

            ViewData["AuthorId"] = new SelectList(_context.Users, "Id", "Id", comment.AuthorId);
            ViewData["ModeratorId"] = new SelectList(_context.Users, "Id", "Id", comment.ModeratorId);
            ViewData["PostId"] = new SelectList(_context.Posts, "Id", "Abstract", comment.PostId);

            return View(comment);
        }

        // POST: Comments/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Body")] Comment comment)
        {
            if (id != comment.Id)
            {
                return NotFound();
            }

            var currentComment = await _context.Comments.Include(c => c.Post).FirstOrDefaultAsync(c => c.Id == comment.Id);

            if (currentComment == null)
            {
                return NotFound();
            }

            if (!CanEdit(currentComment))
            {
                return Forbid();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    currentComment.Body = comment.Body;
                    currentComment.Updated = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CommentExists(comment.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction("Details", "Posts", new { slug = currentComment.Post.Slug }, "comment-section");
            }

            ViewData["AuthorId"] = new SelectList(_context.Users, "Id", "Id", comment.AuthorId);
            ViewData["ModeratorId"] = new SelectList(_context.Users, "Id", "Id", comment.ModeratorId);
            ViewData["PostId"] = new SelectList(_context.Posts, "Id", "Abstract", comment.PostId);

            return View(comment);
        }

        [HttpPost]
        [Authorize(Roles = "Moderator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Moderate(int id, [Bind("Id,ModeratedBody,ModerationType")] Comment comment)
        {
            if (id != comment.Id)
            {
                return NotFound();
            }

            ModelState.Remove(nameof(Comment.Body));

            if (ModelState.IsValid)
            {
                var currentComment = await _context.Comments.Include(c => c.Post).FirstOrDefaultAsync(c => c.Id == comment.Id);

                if (currentComment == null)
                {
                    return NotFound();
                }

                try
                {
                    currentComment.ModeratedBody = comment.ModeratedBody;
                    currentComment.ModerationType = comment.ModerationType;
                    currentComment.Moderated = DateTime.UtcNow;
                    currentComment.ModeratorId = _userManager.GetUserId(User);

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CommentExists(comment.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

                return RedirectToAction("Details", "Posts", new { slug = currentComment.Post.Slug }, "comment-section");
            }

            return View(comment);
        }

        // GET: Comments/Delete/5
        [Authorize(Roles = "Moderator")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var comment = await _context.Comments
                .Include(c => c.Author)
                .Include(c => c.Moderator)
                .Include(c => c.Post)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (comment == null)
            {
                return NotFound();
            }

            return View(comment);
        }

        // POST: Comments/Delete/5
        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Moderator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, string slug)
        {
            var comment = await _context.Comments.FindAsync(id);

            if (comment == null)
            {
                return NotFound();
            }

            comment.Deleted = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return RedirectToAction("Details", "Posts", new { slug }, "comment-section");
        }

        private async Task NotifyAdminAsync(Post post, Comment comment)
        {
            if (string.IsNullOrWhiteSpace(_mailSettings.Mail))
            {
                return;
            }

            var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();

            if (!await _mailCooldown.TryBeginAsync(MailCooldown.Comment, _mailSettings.Mail, remoteIp, MailCooldown.ContactWindow))
            {
                return;
            }

            try
            {
                var author = await _userManager.FindByIdAsync(comment.AuthorId);
                var name = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(author?.FullName) ? "A reader" : author.FullName);
                var email = WebUtility.HtmlEncode(author?.Email ?? string.Empty);
                var title = WebUtility.HtmlEncode(post.Title);
                var body = WebUtility.HtmlEncode(comment.Body);
                var link = PublicUrl.ForPath(Request, PublicUrl.ForPost(post.Slug));
                var html = $"{name} ({email}) commented on <b>{title}</b>.<br/><br/>{body}<br/><br/><a href=\"{WebUtility.HtmlEncode(link)}\">{WebUtility.HtmlEncode(link)}</a>";

                await _emailSender.SendEmailAsync(_mailSettings.Mail, "New comment on " + post.Title, html);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Comment notification could not be sent.");
                await _mailCooldown.ReleaseAsync(MailCooldown.Comment, _mailSettings.Mail, remoteIp);
            }
        }

        private bool CanRead(Post post)
        {
            if (post.Status == ReadyStatus.Production || post.Status == ReadyStatus.Preview)
            {
                return true;
            }

            return post.AuthorId == _userManager.GetUserId(User)
                || User.IsInRole(BlogRole.Administrator.ToString());
        }

        private bool CanEdit(Comment comment)
        {
            return comment.Moderated == null
                && comment.Deleted == null
                && comment.AuthorId == _userManager.GetUserId(User);
        }

        private bool CommentExists(int id)
        {
            return _context.Comments.Any(e => e.Id == id);
        }
    }
}
