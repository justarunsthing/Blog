using Microsoft.AspNetCore.Http;

namespace Blog.Services
{
    public static class PublicUrl
    {
        public const string SiteName = "justarunsthing's Tech Blog";

        public static string Root(HttpRequest request)
        {
            return $"{request.Scheme}://{request.Host}/";
        }

        public static string ForPost(string slug)
        {
            return "/BlogPosts/" + slug;
        }

        public static string ForPath(HttpRequest request, string path)
        {
            if (string.IsNullOrEmpty(path) || path == "/")
            {
                return Root(request);
            }

            if (!path.StartsWith('/'))
            {
                path = "/" + path;
            }

            return $"{request.Scheme}://{request.Host}{path}";
        }

        public static DateTime AsUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }
    }
}
