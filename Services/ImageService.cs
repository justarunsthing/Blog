using Blog.Interfaces;

namespace Blog.Services
{
    public class ImageService : IImageService
    {
        public string ContentType(IFormFile file)
        {
            return file?.ContentType;
        }

        public string DecodeImage(byte[] data, string type)
        {
            if (data == null || string.IsNullOrWhiteSpace(type))
                return null;

            var mime = type.Contains('/') ? type : MimeFromExtension(type);

            return $"data:{mime};base64,{Convert.ToBase64String(data)}";
        }

        private static string MimeFromExtension(string type)
        {
            switch (type.Trim().TrimStart('.').ToLowerInvariant())
            {
                case "png":
                    return "image/png";
                case "gif":
                    return "image/gif";
                case "webp":
                    return "image/webp";
                case "bmp":
                    return "image/bmp";
                default:
                    return "image/jpeg";
            }
        }

        public async Task<byte[]> EncodeImageAsync(IFormFile file)
        {
            if (file == null)
                return null;

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            return ms.ToArray();
        }

        public async Task<byte[]> EncodeImageAsync(string fileName)
        {
            var file = $"{Directory.GetCurrentDirectory()}/wwwroot/img/{fileName}";

            return await File.ReadAllBytesAsync(file);
        }

        public int Size(IFormFile file)
        {
            return Convert.ToInt32(file?.Length);
        }
    }
}
