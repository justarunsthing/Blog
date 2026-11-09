using Blog.Data;
using Blog.Services;
using Microsoft.EntityFrameworkCore;

namespace Blog.Tests;

public class SlugServiceTests
{
    [Fact]
    public void UrlFriendly_maps_CSharp_to_a_public_slug()
    {
        var slugs = CreateSlugService();

        Assert.Equal("c-sharp", slugs.UrlFriendly("C#"));
        Assert.Equal("learn-c-sharp", slugs.UrlFriendly("Learn C#"));
    }

    [Fact]
    public void UrlFriendly_maps_plus_to_plus()
    {
        var slugs = CreateSlugService();

        Assert.Equal("-plus", slugs.UrlFriendly("+"));
        Assert.Equal("c-plus-plus", slugs.UrlFriendly("C++"));
    }

    private static SlugService CreateSlugService()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        return new SlugService(new ApplicationDbContext(options));
    }
}
