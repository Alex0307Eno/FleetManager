using Microsoft.Extensions.Caching.Memory;
using System;

namespace LineBotService.Helpers
{
    public class RateLimiter
    {
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());

        private readonly int _limit;
        private readonly TimeSpan _window;

        public RateLimiter(int limit = 100, int windowSeconds = 60)
        {
            _limit = limit;
            _window = TimeSpan.FromSeconds(windowSeconds);
        }

        public bool IsAllowed(string key)
        {
            if (!_cache.TryGetValue(key, out int count))
            {
                // 第一次進來 → 設 1 並設定過期時間
                _cache.Set(key, 1, _window);
                return true;
            }

            if (count >= _limit)
            {
                return false; // 超過限制
            }

            _cache.Set(key, count + 1, _window);
            return true;
        }
    }
}
