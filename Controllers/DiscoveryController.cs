using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Blog.Data;
using Blog.Enums;
using Blog.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Blog.Controllers
{
    public class DiscoveryController : Controller
    {
        public const int FeedSize = 50;

        private readonly ApplicationDbContext _context;

        public DiscoveryController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("/feed")]
        public async Task<IActionResult> Feed()
        {
            var posts = await _context.Posts
                .AsNoTracking()
                .Where(post => post.Status == ReadyStatus.Production)
                .OrderByDescending(post => post.Published ?? post.Created)
                .Take(FeedSize)
                .Select(post => new
                {
                    post.Title,
                    post.Slug,
                    post.Abstract,
                    post.Published,
                    post.Created
                })
                .ToListAsync();

            var channel = new XElement("channel",
                new XElement("title", PublicUrl.SiteName),
                new XElement("link", PublicUrl.Root(Request)),
                new XElement("description", PublicUrl.SiteName));

            foreach (var post in posts)
            {
                var when = PublicUrl.AsUtc(post.Published ?? post.Created);
                channel.Add(new XElement("item",
                    new XElement("title", post.Title),
                    new XElement("link", PublicUrl.ForPath(Request, "/BlogPosts/UrlFriendly/" + post.Slug)),
                    new XElement("description", post.Abstract ?? string.Empty),
                    new XElement("pubDate", when.ToString("R", CultureInfo.InvariantCulture))));
            }

            var document = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("rss", new XAttribute("version", "2.0"), channel));

            return Content(ToXml(document), "application/rss+xml; charset=utf-8");
        }

        [HttpGet("/sitemap.xml")]
        public async Task<IActionResult> Sitemap()
        {
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
            var urlset = new XElement(ns + "urlset");
            urlset.Add(UrlElement(ns, PublicUrl.Root(Request), null));

            var blogIds = await _context.Blogs
                .AsNoTracking()
                .Where(blog => blog.Posts.Any(post => post.Status == ReadyStatus.Production))
                .OrderBy(blog => blog.Id)
                .Select(blog => blog.Id)
                .ToListAsync();

            foreach (var blogId in blogIds)
            {
                urlset.Add(UrlElement(ns, PublicUrl.ForPath(Request, $"/Posts/BlogPostIndex/{blogId}"), null));
            }

            var posts = await _context.Posts
                .AsNoTracking()
                .Where(post => post.Status == ReadyStatus.Production)
                .OrderByDescending(post => post.Published ?? post.Created)
                .Select(post => new
                {
                    post.Slug,
                    post.Published,
                    post.Created
                })
                .ToListAsync();

            foreach (var post in posts)
            {
                var when = PublicUrl.AsUtc(post.Published ?? post.Created);
                urlset.Add(UrlElement(
                    ns,
                    PublicUrl.ForPath(Request, "/BlogPosts/UrlFriendly/" + post.Slug),
                    when.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
            }

            var document = new XDocument(new XDeclaration("1.0", "utf-8", null), urlset);

            return Content(ToXml(document), "application/xml; charset=utf-8");
        }

        private static XElement UrlElement(XNamespace ns, string loc, string lastmod)
        {
            var url = new XElement(ns + "url", new XElement(ns + "loc", loc));

            if (!string.IsNullOrEmpty(lastmod))
            {
                url.Add(new XElement(ns + "lastmod", lastmod));
            }

            return url;
        }

        private static string ToXml(XDocument document)
        {
            using var writer = new Utf8StringWriter();
            document.Save(writer);
            return writer.ToString();
        }

        private sealed class Utf8StringWriter : StringWriter
        {
            public override Encoding Encoding => Encoding.UTF8;
        }
    }
}
