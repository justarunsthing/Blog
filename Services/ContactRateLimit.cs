using System.Collections.Concurrent;

namespace Blog.Services
{
    public class ContactRateLimit
    {
        public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);
        private readonly ConcurrentDictionary<string, DateTimeOffset> _sent = new(StringComparer.OrdinalIgnoreCase);

        public bool TryBeginSend(string email, DateTimeOffset now)
        {
            var key = email.Trim();

            while (true)
            {
                if (!_sent.TryGetValue(key, out var sent))
                {
                    if (_sent.TryAdd(key, now))
                    {
                        return true;
                    }

                    continue;
                }

                if (now - sent < Window)
                {
                    return false;
                }

                if (_sent.TryUpdate(key, now, sent))
                {
                    return true;
                }
            }
        }
    }
}
