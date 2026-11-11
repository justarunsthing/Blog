using Blog.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Blog.Tests;

public class StubImageService : IImageService
{
    public Task<ImageUpload> ReadUploadAsync(IFormFile file) => Task.FromResult(ImageUpload.None());

    public string ContentType(IFormFile file) => "image/png";

    public string DecodeImage(byte[] data, string type) => "";

    public Task<byte[]> EncodeImageAsync(IFormFile file) => Task.FromResult(Array.Empty<byte>());

    public Task<byte[]> EncodeImageAsync(string fileName) => Task.FromResult(Array.Empty<byte>());

    public int Size(IFormFile file) => 0;
}
