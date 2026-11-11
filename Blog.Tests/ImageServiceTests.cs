using Blog.Services;
using Microsoft.AspNetCore.Http;

namespace Blog.Tests;

public class ImageServiceTests
{
    private readonly ImageService _images = new();

    [Fact]
    public async Task ReadUploadAsync_AcceptsJpegBytesWhenTheBrowserTypeIsWrong()
    {
        var file = Upload(new byte[] { 0xFF, 0xD8, 0xFF, 0x00 }, "text/plain");

        var upload = await _images.ReadUploadAsync(file);

        Assert.True(upload.IsAllowed);
        Assert.Equal("image/jpeg", upload.ContentType);
        Assert.Equal("image/jpeg", _images.ContentType(file));
    }

    [Fact]
    public async Task ReadUploadAsync_AcceptsPngGifAndWebpFromTheirBytes()
    {
        var samples = new Dictionary<string, byte[]>
        {
            ["image/png"] = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A },
            ["image/gif"] = "GIF89a"u8.ToArray(),
            ["image/webp"] = "RIFF\0\0\0\0WEBP"u8.ToArray()
        };

        foreach (var sample in samples)
        {
            var upload = await _images.ReadUploadAsync(Upload(sample.Value, "application/octet-stream"));

            Assert.True(upload.IsAllowed);
            Assert.Equal(sample.Key, upload.ContentType);
        }
    }

    [Fact]
    public async Task ReadUploadAsync_RejectsAFileThatOnlyClaimsToBeAnImage()
    {
        var file = Upload(System.Text.Encoding.ASCII.GetBytes("%PDF-1.7"), "image/jpeg");

        var upload = await _images.ReadUploadAsync(file);

        Assert.False(upload.IsAllowed);
        Assert.Null(upload.Data);
        Assert.Null(await _images.EncodeImageAsync(file));
        Assert.Equal(ImageService.RejectedImageMessage, upload.Error);
    }

    [Fact]
    public async Task ReadUploadAsync_RejectsAnImageLargerThanTheCapBeforeReturningBytes()
    {
        var stream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF });
        var file = new FormFile(stream, 0, ImageService.MaxImageBytes + 1, "Image", "large.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var upload = await _images.ReadUploadAsync(file);

        Assert.False(upload.IsAllowed);
        Assert.Null(upload.Data);
        Assert.Equal(0, stream.Position);
    }

    private static FormFile Upload(byte[] bytes, string contentType)
    {
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "Image", "upload.bin")
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
