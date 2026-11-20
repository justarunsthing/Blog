using Blog.Models;

namespace Blog.ViewModels
{
    public class PostDetailViewModel
    {
        public Post Post { get; set; }
        public string BlogName { get; set; }
        public PostNeighbor Previous { get; set; }
        public PostNeighbor Next { get; set; }
        public List<string> Tags { get; set; } = [];
    }

    public class PostNeighbor
    {
        public string Title { get; set; }
        public string Slug { get; set; }
    }
}
