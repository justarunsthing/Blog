﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            var applicationDbContext = _context.Posts.Include(p => p.Author).Include(p => p.Blog);

            return View(await applicationDbContext.ToListAsync());
        }

        public async Task<IActionResult> BlogPostIndex(int? id, int? page)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pageNumber = page ?? 1;
            var pageSize = 5;
            var posts = await _context.Posts
                .Where(p => p.BlogId == id && p.Status == ReadyStatus.Production)
                .OrderByDescending(p => p.Created)
                .ToPagedListAsync(pageNumber, pageSize);

            return View(posts);
        }

        public async Task<IActionResult> SearchIndex(int? page, string searchTerm)
        {
            ViewData["SearchTerm"] = searchTerm;

            var pageNumber = page ?? 1;
            var pageSize = 5;
            var posts = _searchService.Search(searchTerm);

            return View(await posts.ToPagedListAsync(pageNumber, pageSize));
        }

        // GET: Posts/Details/{slug}
        public async Task<IActionResult> Details(string slug)
        {
            ViewData["Title"] = "Post Details Page";

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
                return NotFound();
            }

            var viewModel = new PostDetailViewModel
            {
                Post = post,
                Tags = _context.Tags
                        .Select(t => t.Text.ToLower())
                        .Distinct().ToList()
            };

            ViewData["HeaderImage"] = _imageService.DecodeImage(post.ImageData, post.ContentType);
            ViewData["MainText"] = post.Title;
            ViewData["SubText"] = post.Abstract;

            return View(viewModel);
        }

        // GET: Posts/Create
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("BlogId,Title,Abstract,Content,Status,Image")] Post post, List<string> tagValues)
        {
            if (ModelState.IsValid)
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
                    ViewData["TagValues"] = string.Join(",", tagValues);

                    return View(post);
                }

                var authorId = _userManager.GetUserId(User);

                post.AuthorId = authorId;
                post.Slug = slug;
                post.ImageData = await _imageService.EncodeImageAsync(post.Image);
                post.ContentType = _imageService.ContentType(post.Image);
                post.Created = DateTime.UtcNow;

                _context.Add(post);

                await _context.SaveChangesAsync();

                foreach (var tagText in tagValues)
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

            ViewData["BlogId"] = new SelectList(_context.Blogs, "Id", "Description", post.BlogId);

            return View(post);
        }

        // GET: Posts/Edit/5
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

            ViewData["BlogId"] = new SelectList(_context.Blogs, "Id", "Name", post.BlogId);
            ViewData["TagValues"] = string.Join(",", post.Tags.Select(t => t.Text));

            return View(post);
        }

        // POST: Posts/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,BlogId,Title,Abstract,Content,Status")] Post post, IFormFile newImage, List<string> tagValues)
        {
            if (id != post.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var currentPost = await _context.Posts.Include(p => p.Tags).FirstOrDefaultAsync(p => p.Id == post.Id);
                    currentPost.Title = post.Title;
                    currentPost.Abstract = post.Abstract;
                    currentPost.Content = post.Content;
                    currentPost.Status = post.Status;

                    var newSlug = _slugService.UrlFriendly(post.Title);

                    if (newSlug != currentPost.Slug)
                    {
                        if (_slugService.IsUnique(newSlug))
                        {
                            currentPost.Title = post.Title;
                            currentPost.Slug = newSlug;
                        }
                        else
                        {
                            ModelState.AddModelError("Title", "The title you provided cannot be used for a unique slug");
                            ViewData["TagValues"] = string.Join(",", tagValues);

                            return View(post);
                        }
                    }

                    if (newImage != null)
                    {
                        currentPost.ImageData = await _imageService.EncodeImageAsync(newImage);
                        currentPost.ContentType = _imageService.ContentType(newImage);
                    }

                    // Clear any existing tags
                    _context.Tags.RemoveRange(currentPost.Tags);

                    // Add new tags
                    foreach (var tagText in tagValues)
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

            ViewData["AuthorId"] = new SelectList(_context.Users, "Id", "Id", post.AuthorId);
            ViewData["BlogId"] = new SelectList(_context.Blogs, "Id", "Description", post.BlogId);

            return View(post);
        }

        // GET: Posts/Delete/5
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

            return View(post);
        }

        // POST: Posts/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var post = await _context.Posts.FindAsync(id);

            if (post != null)
            {
                _context.Posts.Remove(post);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private bool PostExists(int id)
        {
            return _context.Posts.Any(e => e.Id == id);
        }
    }
}
