using Blog.Data;
using Blog.Enums;
using Blog.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Blog.Controllers
{
    [Route("media")]
    public class MediaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<BlogUser> _userManager;

        public MediaController(ApplicationDbContext context, UserManager<BlogUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet("blogs/{id:int}")]
        public async Task<IActionResult> Blog(int id)
        {
            var blog = await _context.Blogs
                .AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new
                {
                    item.ImageData,
                    item.ContentType,
                    IsPublic = item.Posts.Any(post => post.Status == ReadyStatus.Production)
                })
                .FirstOrDefaultAsync();

            if (blog == null || !CanReadBlog(blog.IsPublic))
            {
                return NotFound();
            }

            return Image(blog.ImageData, blog.ContentType, blog.IsPublic);
        }

        [HttpGet("posts/{id:int}")]
        public async Task<IActionResult> Post(int id)
        {
            var post = await _context.Posts
                .AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new
                {
                    item.ImageData,
                    item.ContentType,
                    item.Status,
                    item.AuthorId
                })
                .FirstOrDefaultAsync();

            if (post == null || !CanReadPost(post.Status, post.AuthorId))
            {
                return NotFound();
            }

            var shared = post.Status == ReadyStatus.Production || post.Status == ReadyStatus.Preview;

            return Image(post.ImageData, post.ContentType, shared);
        }

        [HttpGet("users/{id}")]
        public async Task<IActionResult> UserAvatar(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _context.Users
                .AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new
                {
                    item.ImageData,
                    item.ContentType
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound();
            }

            return Image(user.ImageData, user.ContentType, shared: true);
        }

        private bool CanReadBlog(bool isPublic)
        {
            return isPublic || User.IsInRole(BlogRole.Administrator.ToString());
        }

        private bool CanReadPost(ReadyStatus status, string authorId)
        {
            if (status == ReadyStatus.Production || status == ReadyStatus.Preview)
            {
                return true;
            }

            return authorId == _userManager.GetUserId(User)
                || User.IsInRole(BlogRole.Administrator.ToString());
        }

        private IActionResult Image(byte[] data, string contentType, bool shared)
        {
            var mediaType = ToMediaType(contentType);

            if (data == null || data.Length == 0 || mediaType == null)
            {
                return NotFound();
            }

            var scope = shared ? "public" : "private";
            Response.Headers.CacheControl = $"{scope}, max-age=86400";

            return File(data, mediaType);
        }

        private static string ToMediaType(string contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
            {
                return null;
            }

            if (contentType.Contains('/'))
            {
                return contentType;
            }

            switch (contentType.Trim().TrimStart('.').ToLowerInvariant())
            {
                case "png":
                    return "image/png";
                case "gif":
                    return "image/gif";
                case "webp":
                    return "image/webp";
                case "bmp":
                    return "image/bmp";
                default:
                    return "image/jpeg";
            }
        }
    }
}
