using Blog.Models;

namespace Blog.ViewModels
{
    public class BlogCard
    {
        public int Id { get; set; }
        public string AuthorId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Updated { get; set; }
        public string ContentType { get; set; }
        public DateTime LatestPublished { get; set; }
        public BlogUser Author { get; set; }
    }
}
