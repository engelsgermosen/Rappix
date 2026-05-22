using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace Rappix.BuildingBlocks.Observability.Extensions;

public static class LoggingExtensions
{
    /// <summary>
    /// Configura Serilog con consola + Seq + enrichers estándar.
    /// Llamar antes de Build() en Program.cs.
    /// </summary>
    public static IHostBuilder UseRappixLogging(this IHostBuilder hostBuilder, string serviceName)
    {
        return hostBuilder.UseSerilog((context, services, configuration) =>
        {
            var seqUrl = context.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341";

            // El nivel de MassTransit es configurable via Logging:LogLevel:MassTransit (mismos nombres que MEL).
            // Default Information (comportamiento previo); en Development se sube a Debug para ver el movimiento
            // de mensajes (SEND/RECEIVE/Consume) — esencial al diagnosticar por que una saga no avanza.
            LogEventLevel massTransitLevel =
                Enum.TryParse(context.Configuration["Logging:LogLevel:MassTransit"], ignoreCase: true, out LogEventLevel parsedLevel)
                    ? parsedLevel
                    : LogEventLevel.Information;

            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .Enrich.WithProperty("Service", serviceName)
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .MinimumLevel.Override("MassTransit", massTransitLevel)
                .WriteTo.Console(outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] [{Service}] {Message:lj} {Properties:j}{NewLine}{Exception}")
                .WriteTo.Seq(seqUrl);
        });
    }

    /// <summary>
    /// Agrega middleware de Serilog request logging con enriquecimiento de claims, status, etc.
    /// </summary>
    public static IApplicationBuilder UseRappixRequestLogging(this IApplicationBuilder app)
    {
        return app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate =
                "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
                diagnosticContext.Set("UserId", httpContext.User.FindFirst("sub")?.Value ?? "anonymous");

                if (httpContext.Request.Headers.TryGetValue("X-Correlation-Id", out var correlationId))
                {
                    diagnosticContext.Set("CorrelationId", correlationId.ToString());
                }
            };
        });
    }
}
