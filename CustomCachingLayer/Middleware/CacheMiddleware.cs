using System.Text;

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
