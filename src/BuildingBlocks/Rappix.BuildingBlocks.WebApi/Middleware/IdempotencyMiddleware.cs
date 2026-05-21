using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Rappix.BuildingBlocks.WebApi.Middleware;

/// <summary>
/// Middleware que detecta el header Idempotency-Key en peticiones POST/PUT/PATCH
/// y previene procesar la misma operacion dos veces (clave guardada en Redis por 24h).
/// Esencial para pagos, ordenes y cualquier operacion con efectos colaterales.
/// </summary>
public sealed partial class IdempotencyMiddleware(
    RequestDelegate next,
    ILogger<IdempotencyMiddleware> logger)
{
    private const string HeaderName = "Idempotency-Key";
    private static readonly TimeSpan KeyLifetime = TimeSpan.FromHours(24);

    public async Task InvokeAsync(HttpContext context, IDistributedCache cache)
    {
        var method = context.Request.Method;
        var isMutating = HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method);

        if (!isMutating || !context.Request.Headers.TryGetValue(HeaderName, out var keyValues))
        {
            await next(context);
            return;
        }

        var key = keyValues.ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            await next(context);
            return;
        }

        var cacheKey = $"idempotency:{key}";
        var existing = await cache.GetStringAsync(cacheKey, context.RequestAborted);

        if (existing is not null)
        {
            LogDuplicateRequest(logger, key);
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Operacion duplicada",
                detail = "Una peticion con esta IdempotencyKey ya fue procesada."
            }, context.RequestAborted);
            return;
        }

        await cache.SetStringAsync(
            cacheKey,
            DateTime.UtcNow.ToString("O"),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = KeyLifetime },
            context.RequestAborted);

        await next(context);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Peticion idempotente duplicada detectada: {Key}")]
    private static partial void LogDuplicateRequest(ILogger logger, string key);
}

public static class IdempotencyMiddlewareExtensions
{
    public static IApplicationBuilder UseIdempotency(this IApplicationBuilder app)
        => app.UseMiddleware<IdempotencyMiddleware>();
}