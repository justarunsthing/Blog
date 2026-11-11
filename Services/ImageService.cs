using Blog.Interfaces;

namespace Blog.Services
{
    public class ImageService : IImageService
    {
        public const int MaxImageBytes = 2 * 1024 * 1024;
        public const long MaxRequestBytes = MaxImageBytes + (6 * 1024 * 1024);
        public const string RejectedImageMessage = "Use a jpeg, png, gif, or webp image that is 2 MB or smaller.";

        public async Task<ImageUpload> ReadUploadAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return ImageUpload.None();
            }

            if (file.Length > MaxImageBytes)
            {
                return ImageUpload.Reject(RejectedImageMessage);
            }

            var buffer = new byte[file.Length];

            await using var stream = file.OpenReadStream();
            var offset = 0;

            while (offset < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(offset));

                if (read == 0)
                {
                    break;
                }

                offset += read;
            }

            var extra = new byte[1];

            if (offset == 0 || await stream.ReadAsync(extra) > 0)
            {
                return ImageUpload.Reject(RejectedImageMessage);
            }

            var data = offset == buffer.Length ? buffer : buffer[..offset];
            var contentType = DetectContentType(data);

            if (contentType == null)
            {
                return ImageUpload.Reject(RejectedImageMessage);
            }

            return ImageUpload.Ok(data, contentType);
        }

        public string ContentType(IFormFile file)
        {
            if (file == null || file.Length <= 0 || file.Length > MaxImageBytes)
            {
                return null;
            }

            var prefixLength = (int)Math.Min(file.Length, 12);
            var prefix = new byte[prefixLength];

            using var stream = file.OpenReadStream();
            var read = 0;

            while (read < prefix.Length)
            {
                var count = stream.Read(prefix, read, prefix.Length - read);

                if (count == 0)
                {
                    break;
                }

                read += count;
            }

            if (read < prefix.Length)
            {
                return null;
            }

            return DetectContentType(prefix);
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
            var upload = await ReadUploadAsync(file);

            return upload.IsAllowed ? upload.Data : null;
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

        private static string DetectContentType(byte[] data)
        {
            if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            {
                return "image/jpeg";
            }

            if (data.Length >= 8
                && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47
                && data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A)
            {
                return "image/png";
            }

            if (data.Length >= 6
                && data[0] == (byte)'G' && data[1] == (byte)'I' && data[2] == (byte)'F'
                && data[3] == (byte)'8' && (data[4] == (byte)'7' || data[4] == (byte)'9') && data[5] == (byte)'a')
            {
                return "image/gif";
            }

            if (data.Length >= 12
                && data[0] == (byte)'R' && data[1] == (byte)'I' && data[2] == (byte)'F' && data[3] == (byte)'F'
                && data[8] == (byte)'W' && data[9] == (byte)'E' && data[10] == (byte)'B' && data[11] == (byte)'P')
            {
                return "image/webp";
            }

            return null;
        }
    }
}
