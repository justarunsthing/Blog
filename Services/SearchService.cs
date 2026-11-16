using Blog.Data;
using Blog.Enums;
using Blog.Models;
using Microsoft.EntityFrameworkCore;

namespace Blog.Services
{
    public class SearchService(ApplicationDbContext context)
    {
        private readonly ApplicationDbContext _context = context;

        public IQueryable<Post> Search(string searchTerm)
        {
            var posts = _context.Posts.AsNoTracking().Where(p => p.Status == ReadyStatus.Production);

            if (searchTerm != null)
            {
                searchTerm = searchTerm.ToLower();

                posts = posts.Where(
                    p => p.Title.ToLower().Contains(searchTerm) ||
                    p.Abstract.ToLower().Contains(searchTerm) ||
                    p.Content.ToLower().Contains(searchTerm));
            }

            return posts
                .OrderByDescending(p => p.Published ?? p.Created)
                .Select(post => new Post
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
    }
}
