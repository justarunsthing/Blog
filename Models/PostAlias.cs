namespace Blog.Models
{
    public class PostAlias
    {
        public int Id { get; set; }
        public int PostId { get; set; }
        public string Slug { get; set; }

        public virtual Post Post { get; set; }
    }
}
