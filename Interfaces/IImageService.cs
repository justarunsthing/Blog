namespace Blog.Interfaces
{
    public class ImageUpload
    {
        public bool IsAllowed { get; init; }
        public byte[] Data { get; init; }
        public string ContentType { get; init; }
        public string Error { get; init; }

        public static ImageUpload None() => new() { IsAllowed = true };

        public static ImageUpload Ok(byte[] data, string contentType) =>
            new() { IsAllowed = true, Data = data, ContentType = contentType };

        public static ImageUpload Reject(string error) => new() { IsAllowed = false, Error = error };
    }

    public interface IImageService
    {
        Task<ImageUpload> ReadUploadAsync(IFormFile file);
        Task<byte[]> EncodeImageAsync(IFormFile file);
        Task<byte[]> EncodeImageAsync(string fileName);
        string DecodeImage(byte[] data, string type);
        string ContentType(IFormFile file);
        int Size(IFormFile file);
    }
}
