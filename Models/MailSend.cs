namespace Blog.Models
{
    public class MailSend
    {
        public int Id { get; set; }
        public string Purpose { get; set; }
        public string Email { get; set; }
        public string RemoteIp { get; set; }
        public DateTime Created { get; set; }
    }
}
