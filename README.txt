To create a custom caching layer for a Web API in .NET Core that uses MSSQL for storing responses, we'll need to focus on a few key components:

Cache Database Design: A table to store cached responses with an expiration time.
Cache Middleware: Intercepts requests and checks the cache before proceeding to the controller.
Cache Service: Handles interactions with the cache database.
Cache Cleanup: Regularly removes expired cache entries.
Here's a step-by-step implementation:

Step 1: Cache Database Design
First, we'll create a SQL table to store the cache entries.

CREATE TABLE CachedResponses (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    CacheKey NVARCHAR(450) NOT NULL,
    Response NVARCHAR(MAX) NOT NULL,
    CreatedAt DATETIME NOT NULL,
    ExpiresAt DATETIME NOT NULL
);

CREATE INDEX IX_CachedResponses_CacheKey ON CachedResponses(CacheKey);
CREATE INDEX IX_CachedResponses_ExpiresAt ON CachedResponses(ExpiresAt);

Step 2: Middleware Cache
Develop a middleware that receives incoming requests, checks the cache, and then continues.

public class CacheMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ICacheService _cacheService;

    public CacheMiddleware(RequestDelegate next, ICacheService cacheService)
    {
        _next = next;
        _cacheService = cacheService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var cacheKey = GenerateCacheKeyFromRequest(context.Request);

        var cachedResponse = await _cacheService.GetCachedResponseAsync(cacheKey);
        if (cachedResponse != null)
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(cachedResponse);
            return;
        }

        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        await _next(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseText = await new StreamReader(context.Response.Body).ReadToEndAsync();
        context.Response.Body.Seek(0, SeekOrigin.Begin);

        await _cacheService.CacheResponseAsync(cacheKey, responseText, TimeSpan.FromHours(2));
        await responseBody.CopyToAsync(originalBodyStream);
    }

    private string GenerateCacheKeyFromRequest(HttpRequest request)
    {
        var keyBuilder = new StringBuilder();
        keyBuilder.Append($"{request.Path}");

        foreach (var (key, value) in request.Query.OrderBy(k => k.Key))
        {
            keyBuilder.Append($"|{key}-{value}");
        }

        return keyBuilder.ToString();
    }
}

Step 3: Utilizing Cache Services
To manage interactions with the cache database, create a service.

public interface ICacheService
{
    Task<string> GetCachedResponseAsync(string cacheKey);
    Task CacheResponseAsync(string cacheKey, string response, TimeSpan timeToLive);
    Task RemoveExpiredCacheEntriesAsync();
}

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

Step 4: Clearing the Cache
Establish a background service to clear out expired cache items on a regular basis.

public class CacheCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);

    public CacheCleanupService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

            await cacheService.RemoveExpiredCacheEntriesAsync();
            await Task.Delay(_cleanupInterval, stoppingToken);
        }
    }
}

Step 5: Services and Middleware Registration
In the Program class, register the middleware and services.

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<ICacheService>(provider => new CacheService(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHostedService<CacheCleanupService>();
builder.Services.AddControllers();

var app = builder.Build();

if (builder.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();
app.UseRouting();

app.UseMiddleware<CacheMiddleware>();

app.Run();
