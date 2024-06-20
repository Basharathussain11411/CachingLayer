using Microsoft.Data.SqlClient;

public class CacheService : ICacheService
{
    private readonly string _connectionString;

    public CacheService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<string> GetCachedResponseAsync(string cacheKey)
    {
        using var connection = new SqlConnection(_connectionString);
        var command = new SqlCommand("SELECT Response FROM CachedResponses WHERE CacheKey = @CacheKey AND ExpiresAt > GETDATE()", connection);
        command.Parameters.AddWithValue("@CacheKey", cacheKey);

        await connection.OpenAsync();
        var result = await command.ExecuteScalarAsync();
        return result as string;
    }

    public async Task CacheResponseAsync(string cacheKey, string response, TimeSpan timeToLive)
    {
        var expiresAt = DateTime.UtcNow.Add(timeToLive);

        using var connection = new SqlConnection(_connectionString);
        var command = new SqlCommand(
            "INSERT INTO CachedResponses (CacheKey, Response, CreatedAt, ExpiresAt) VALUES (@CacheKey, @Response, GETDATE(), @ExpiresAt)",
            connection);
        command.Parameters.AddWithValue("@CacheKey", cacheKey);
        command.Parameters.AddWithValue("@Response", response);
        command.Parameters.AddWithValue("@ExpiresAt", expiresAt);

        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();
    }

    public async Task RemoveExpiredCacheEntriesAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        var command = new SqlCommand("DELETE FROM CachedResponses WHERE ExpiresAt <= GETDATE()", connection);

        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();
    }
}
