using Blog.Data;
using Blog.Enums;
using Blog.Models;

namespace Blog.Services
{
    public class SearchService(ApplicationDbContext context)
    {
        private readonly ApplicationDbContext _context = context;

        public IQueryable<Post> Search(string searchTerm)
        {
            var posts = _context.Posts.Where(p => p.Status == ReadyStatus.Production).AsQueryable();

            if (searchTerm != null)
            {
                searchTerm = searchTerm.ToLower();

                posts = posts.Where(
                    p => p.Title.ToLower().Contains(searchTerm) ||
                    p.Abstract.ToLower().Contains(searchTerm) ||
                    p.Content.ToLower().Contains(searchTerm));
            }

            return posts.OrderByDescending(p => p.Created);
        }
    }
}
