using Blog.ViewModels;
using Microsoft.Extensions.Options;

namespace Blog.Services
{
    public class SiteClock
    {
        private readonly TimeZoneInfo _zone;

        public SiteClock(IOptions<SiteSettings> settings)
        {
            var id = string.IsNullOrWhiteSpace(settings.Value.TimeZone)
                ? "Europe/London"
                : settings.Value.TimeZone;
            _zone = TimeZoneInfo.FindSystemTimeZoneById(id);
        }

        public DateTime ToLocal(DateTime value)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(PublicUrl.AsUtc(value), _zone);
        }

        public string Format(DateTime value)
        {
            return ToLocal(value).ToString("dd MMM yyyy");
        }

        public string FormatTime(DateTime value)
        {
            return ToLocal(value).ToString("dd MMM yyyy HH:mm");
        }

        public string FormatDay(DateTime value)
        {
            return ToLocal(value).ToString("yyyy-MM-dd");
        }

        public bool WasRevised(DateTime published, DateTime updated)
        {
            return PublicUrl.AsUtc(updated) - PublicUrl.AsUtc(published) > TimeSpan.FromSeconds(2);
        }
    }
}
