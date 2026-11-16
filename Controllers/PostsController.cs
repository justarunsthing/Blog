using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Blog.Data;
using Blog.Models;
using Blog.Interfaces;
using Microsoft.AspNetCore.Identity;
using Blog.Enums;
using X.PagedList.EF;
using Blog.Services;
using Blog.ViewModels;

namespace Blog.Controllers
{
    public class PostsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ISlugService _slugService;
        private readonly IImageService _imageService;
        private readonly UserManager<BlogUser> _userManager;
        private readonly SearchService _searchService;

        public PostsController(ApplicationDbContext context, ISlugService slugService, IImageService imageService, UserManager<BlogUser> userManager, SearchService searchService)
        {
            _context = context;
            _slugService = slugService;
            _imageService = imageService;
            _userManager = userManager;
            _searchService = searchService;
        }

        // GET: Posts
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var isAdministrator = User.IsInRole(BlogRole.Administrator.ToString());

            IQueryable<Post> posts = _context.Posts.AsNoTracking();

            if (!isAdministrator)
            {
                posts = posts.Where(p =>
                    p.Status == ReadyStatus.Production ||
                    (userId != null && p.AuthorId == userId));
            }

            return View(await ProjectList(posts.OrderByDescending(p => p.Created)).ToListAsync());
        }

        public async Task<IActionResult> BlogPostIndex(int? id, int? page)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pageNumber = page ?? 1;
            var pageSize = 5;
            var posts = ProjectList(_context.Posts
                .AsNoTracking()
                .Where(p => p.BlogId == id && p.Status == ReadyStatus.Production)
                .OrderByDescending(p => p.Published ?? p.Created));

            return View(await posts.ToPagedListAsync(pageNumber, pageSize));
        }

        public async Task<IActionResult> SearchIndex(int? page, string searchTerm)
        {
            ViewData["SearchTerm"] = searchTerm;

            var pageNumber = page ?? 1;
            var pageSize = 5;
            var posts = _searchService.Search(searchTerm);

            return View(await posts.ToPagedListAsync(pageNumber, pageSize));
        }

        public async Task<IActionResult> TagIndex(string tag, int? page)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return NotFound();
            }

            var pageNumber = page ?? 1;
            var pageSize = 5;
            var normalized = tag.ToLower();
            var posts = ProjectList(_context.Posts
                .AsNoTracking()
                .Where(p => p.Status == ReadyStatus.Production && p.Tags.Any(t => t.Text.ToLower() == normalized))
                .OrderByDescending(p => p.Published ?? p.Created));

            ViewData["Tag"] = tag;

            return View(await posts.ToPagedListAsync(pageNumber, pageSize));
        }

        // GET: Posts/Details/{slug}
        public async Task<IActionResult> Details(string slug)
        {
            if (string.IsNullOrEmpty(slug))
            {
                return NotFound();
            }

            var post = await _context.Posts
                .Include(p => p.Author)
                .Include(p => p.Tags)
                .Include(p => p.Comments)
                .ThenInclude(c => c.Author) // Grabs authors of the comments
                .Include(p => p.Comments)
                .ThenInclude(c => c.Moderator)
                .FirstOrDefaultAsync(m => m.Slug == slug);

            if (post == null)
            {
                var alias = await _context.PostAliases
                    .Include(postAlias => postAlias.Post)
                    .FirstOrDefaultAsync(postAlias => postAlias.Slug == slug);

                if (alias?.Post == null || !CanRead(alias.Post))
                {
                    return NotFound();
                }

                return new RedirectResult($"/BlogPosts/UrlFriendly/{alias.Post.Slug}", permanent: true);
            }

            if (!CanRead(post))
            {
                return NotFound();
            }

            var viewModel = new PostDetailViewModel
            {
                Post = post,
                Tags = await _context.Tags
                        .Where(t => t.Post.Status == ReadyStatus.Production)
                        .Select(t => t.Text.ToLower())
                        .Distinct()
                        .ToListAsync()
            };

            ViewData["Title"] = post.Title;
            ViewData["MainText"] = post.Title;
            ViewData["SubText"] = post.Abstract;

            if (post.Status == ReadyStatus.Production)
            {
                var canonical = PublicUrl.ForPath(Request, "/BlogPosts/UrlFriendly/" + post.Slug);
                ViewData["CanonicalUrl"] = canonical;
                ViewData["MetaDescription"] = post.Abstract;
                ViewData["OgTitle"] = post.Title;
                ViewData["OgDescription"] = post.Abstract;
                ViewData["OgUrl"] = canonical;
                ViewData["OgType"] = "article";

                if (!string.IsNullOrEmpty(post.ContentType))
                {
                    ViewData["OgImage"] = PublicUrl.ForPath(Request, $"/media/posts/{post.Id}");
                }
            }

            return View(viewModel);
        }

        // GET: Posts/Create
        [Authorize(Roles = "Administrator")]
        public IActionResult Create()
        {
            ViewData["AuthorId"] = new SelectList(_context.Users, "Id", "Id");
            ViewData["BlogId"] = new SelectList(_context.Blogs, "Id", "Name");

            return View();
        }

        // POST: Posts/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("BlogId,Title,Abstract,Content,Status,Image")] Post post, List<string> tagValues)
        {
            var upload = await _imageService.ReadUploadAsync(post.Image);

            if (!upload.IsAllowed)
            {
                ModelState.AddModelError(nameof(post.Image), upload.Error);
            }

            var tagsAreValid = TryNormalizeTags(tagValues, out var tags);

            if (ModelState.IsValid && tagsAreValid)
            {
                var slug = _slugService.UrlFriendly(post.Title);
                var slugError = false;

                if (string.IsNullOrEmpty(slug))
                {
                    slugError = true;
                    ModelState.AddModelError("", "Title cannot be empty for a unique slug");
                }

                if (!_slugService.IsUnique(slug))
                {
                    slugError = true;
                    ModelState.AddModelError("Title", "The title you provided cannot be used for a unique slug");
                }

                if (slugError)
                {
                    SetCreateViewData(post, tagValues);

                    return View(post);
                }

                var authorId = _userManager.GetUserId(User);

                post.AuthorId = authorId;
                post.Slug = slug;
                post.ImageData = upload.Data;
                post.ContentType = upload.ContentType;
                post.Created = DateTime.UtcNow;

                if (post.Status == ReadyStatus.Production && post.Published == null)
                {
                    post.Published = DateTime.UtcNow;
                }

                _context.Add(post);

                await _context.SaveChangesAsync();

                foreach (var tagText in tags)
                {
                    _context.Add(new Tag
                    {
                        PostId = post.Id,
                        AuthorId = authorId,
                        Text = tagText
                    });
                }

                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            SetCreateViewData(post, tagValues);

            return View(post);
        }

        // GET: Posts/Edit/5
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var post = await _context.Posts.Include(p => p.Tags).FirstOrDefaultAsync(p => p.Id == id);

            if (post == null)
            {
                return NotFound();
            }

            if (!CanManage(post))
            {
                return Forbid();
            }

            ViewData["BlogId"] = new SelectList(_context.Blogs, "Id", "Name", post.BlogId);
            ViewData["TagValues"] = string.Join(",", post.Tags.Select(t => t.Text));

            return View(post);
        }

        // POST: Posts/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,BlogId,Title,Abstract,Content,Status")] Post post, IFormFile newImage, List<string> tagValues)
        {
            if (id != post.Id)
            {
                return NotFound();
            }

            var currentPost = await _context.Posts.Include(p => p.Tags).FirstOrDefaultAsync(p => p.Id == post.Id);

            if (currentPost == null)
            {
                return NotFound();
            }

            if (!CanManage(currentPost))
            {
                return Forbid();
            }

            var upload = await _imageService.ReadUploadAsync(newImage);

            if (!upload.IsAllowed)
            {
                ModelState.AddModelError(nameof(post.Image), upload.Error);
            }

            var tagsAreValid = TryNormalizeTags(tagValues, out var tags);

            if (ModelState.IsValid && tagsAreValid && !await _context.Blogs.AnyAsync(blog => blog.Id == post.BlogId))
            {
                ModelState.AddModelError(nameof(post.BlogId), "The selected blog does not exist.");
            }

            if (!ModelState.IsValid || !tagsAreValid)
            {
                SetEditViewData(post, tagValues);

                return View(post);
            }

            var newSlug = _slugService.UrlFriendly(post.Title);
            PostAlias reclaimedAlias = null;

            if (newSlug != currentPost.Slug)
            {
                reclaimedAlias = await _context.PostAliases
                    .FirstOrDefaultAsync(alias => alias.Slug == newSlug && alias.PostId == currentPost.Id);

                if (reclaimedAlias == null && !_slugService.IsUnique(newSlug))
                {
                    ModelState.AddModelError("Title", "The title you provided cannot be used for a unique slug");
                    SetEditViewData(post, tagValues);

                    return View(post);
                }
            }

            try
            {
                currentPost.BlogId = post.BlogId;
                currentPost.Title = post.Title;
                currentPost.Abstract = post.Abstract;
                currentPost.Content = post.Content;
                currentPost.Status = post.Status;

                if (currentPost.Status == ReadyStatus.Production && currentPost.Published == null)
                {
                    currentPost.Published = DateTime.UtcNow;
                }

                if (newSlug != currentPost.Slug)
                {
                    if (reclaimedAlias != null)
                    {
                        _context.PostAliases.Remove(reclaimedAlias);
                    }

                    if (!string.IsNullOrEmpty(currentPost.Slug)
                        && !await _context.PostAliases.AnyAsync(alias => alias.Slug == currentPost.Slug))
                    {
                        _context.PostAliases.Add(new PostAlias
                        {
                            PostId = currentPost.Id,
                            Slug = currentPost.Slug
                        });
                    }

                    currentPost.Slug = newSlug;
                }

                if (upload.Data != null)
                {
                    currentPost.ImageData = upload.Data;
                    currentPost.ContentType = upload.ContentType;
                }

                // Clear any existing tags
                _context.Tags.RemoveRange(currentPost.Tags);

                // Add new tags
                foreach (var tagText in tags)
                {
                    _context.Add(new Tag
                    {
                        PostId = post.Id,
                        AuthorId = currentPost.AuthorId,
                        Text = tagText
                    });
                }

                currentPost.Updated = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PostExists(post.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Posts/Delete/5
        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var post = await _context.Posts
                .Include(p => p.Author)
                .Include(p => p.Blog)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (post == null)
            {
                return NotFound();
            }

            if (!CanManage(post))
            {
                return Forbid();
            }

            return View(post);
        }

        // POST: Posts/Delete/5
        [HttpPost, ActionName("Delete")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var post = await _context.Posts.FindAsync(id);

            if (post == null)
            {
                return NotFound();
            }

            if (!CanManage(post))
            {
                return Forbid();
            }

            _context.Posts.Remove(post);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private bool TryNormalizeTags(IEnumerable<string> tagValues, out List<string> tags)
        {
            tags = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lengthError = false;
            var duplicateError = false;

            foreach (var raw in tagValues ?? Enumerable.Empty<string>())
            {
                var text = (raw ?? string.Empty).Trim();

                if (text.Length == 0)
                {
                    continue;
                }

                if (text.Length < 2 || text.Length > 25)
                {
                    lengthError = true;
                }

                if (!seen.Add(text))
                {
                    duplicateError = true;
                    continue;
                }

                tags.Add(text);
            }

            if (lengthError)
            {
                ModelState.AddModelError(string.Empty, "Each tag must be between 2 and 25 characters.");
            }

            if (duplicateError)
            {
                ModelState.AddModelError(string.Empty, "Tags must be unique.");
            }

            return !lengthError && !duplicateError;
        }

        private void SetCreateViewData(Post post, IEnumerable<string> tagValues)
        {
            ViewData["TagValues"] = string.Join(",", tagValues ?? Enumerable.Empty<string>());
            ViewData["BlogId"] = new SelectList(_context.Blogs, "Id", "Name", post.BlogId);
        }

        private void SetEditViewData(Post post, IEnumerable<string> tagValues)
        {
            ViewData["TagValues"] = string.Join(",", tagValues ?? Enumerable.Empty<string>());
            ViewData["AuthorId"] = new SelectList(_context.Users, "Id", "Id", post.AuthorId);
            ViewData["BlogId"] = new SelectList(_context.Blogs, "Id", "Name", post.BlogId);
        }

        private static IQueryable<Post> ProjectList(IQueryable<Post> posts)
        {
            return posts.Select(post => new Post
            {
                Id = post.Id,
                BlogId = post.BlogId,
                AuthorId = post.AuthorId,
                Title = post.Title,
                Abstract = post.Abstract,
                Created = post.Created,
                Updated = post.Updated,
                Published = post.Published,
                Status = post.Status,
                Slug = post.Slug,
                ContentType = post.ContentType
            });
        }

        private bool CanManage(Post post)
        {
            return post.AuthorId == _userManager.GetUserId(User)
                || User.IsInRole(BlogRole.Administrator.ToString());
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

        private bool PostExists(int id)
        {
            return _context.Posts.Any(e => e.Id == id);
        }
    }
}
