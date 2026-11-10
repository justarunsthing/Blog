using Blog.Data;
using Blog.Enums;
using Blog.Models;
using X.PagedList.EF;
using Blog.ViewModels;
using Blog.Interfaces;
using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Blog.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IBlogEmailSender _emailSender;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, IBlogEmailSender emailSender, ApplicationDbContext context)
        {
            _logger = logger;
            _emailSender = emailSender;
            _context = context;
        }

        public async Task<IActionResult> IndexAsync(int? page)
        {
            var pageNumber = page ?? 1;
            var pageSize = 5;
            var blogs =  _context.Blogs
                .Where(b => b.Posts.Any(p => p.Status == ReadyStatus.Production))
                .OrderByDescending(b => b.Created)
                .Include(b => b.Author)
                .ToPagedListAsync(pageNumber, pageSize);

            return View(await blogs);
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contact(ContactMe model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var name = WebUtility.HtmlEncode(model.Name);
            var email = WebUtility.HtmlEncode(model.Email);
            var phone = WebUtility.HtmlEncode(model.Phone);
            var message = $"{WebUtility.HtmlEncode(model.Message)} <hr/> Phone: {phone}";

            await _emailSender.SendContactEmailAsync(email, model.Subject, name, message);

            return RedirectToAction("Index");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
