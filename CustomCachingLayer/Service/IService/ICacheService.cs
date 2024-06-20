using Microsoft.Data.SqlClient;

public interface ICacheService
{
    Task<string> GetCachedResponseAsync(string cacheKey);
    Task CacheResponseAsync(string cacheKey, string response, TimeSpan timeToLive);
    Task RemoveExpiredCacheEntriesAsync();
}
