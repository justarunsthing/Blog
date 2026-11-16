using Blog.Services;
using Blog.ViewModels;
using Microsoft.Extensions.Options;

namespace Blog.Tests;

public class SiteClockTests
{
    [Fact]
    public void Format_uses_the_site_zone_when_utc_is_still_the_previous_day()
    {
        var clock = new SiteClock(Options.Create(new SiteSettings { TimeZone = "Europe/London" }));
        var utcEvening = new DateTime(2026, 7, 1, 23, 30, 0, DateTimeKind.Utc);

        Assert.Equal("02 Jul 2026", clock.Format(utcEvening));
        Assert.Equal("2026-07-02", clock.FormatDay(utcEvening));
    }
}
