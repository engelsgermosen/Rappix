using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace Rappix.BuildingBlocks.WebApi.Middleware;

/// <summary>
/// Lee o genera un X-Correlation-Id y lo enriquece en el contexto de log de Serilog.
/// Esencial para trazar una petición a través de varios microservicios.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var headerValue)
            && !string.IsNullOrWhiteSpace(headerValue)
            ? headerValue.ToString()
            : Guid.CreateVersion7().ToString();

        context.Response.Headers[HeaderName] = correlationId;
        context.Items[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();
}
