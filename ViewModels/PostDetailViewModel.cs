using Blog.Models;

namespace Blog.ViewModels
{
    public class PostDetailViewModel
    {
        public Post Post { get; set; }
        public List<string> Tags { get; set; } = [];
    }
}
