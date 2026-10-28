using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Rappix.BuildingBlocks.Observability.Extensions;

public static class TracingExtensions
{
    private static readonly string ServiceVersion =
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";

    /// <summary>
    /// Configura trazas y métricas OpenTelemetry (ASP.NET Core, HttpClient, gRPC client, runtime,
    /// más las fuentes nativas de Npgsql y MassTransit) y exporta por OTLP (OTEL_EXPORTER_OTLP_ENDPOINT,
    /// leído automáticamente por el SDK). Llamar junto a UseRappixLogging, antes de Build().
    /// </summary>
    /// <param name="builder">Host builder de la aplicación (WebApplicationBuilder u otro IHostApplicationBuilder).</param>
    /// <param name="serviceName">Nombre del servicio: resource attribute service.name y nombre del ActivitySource/Meter propio.</param>
    /// <param name="instrumentRedis">
    /// Activa la instrumentación de StackExchange.Redis. Requiere un <c>IConnectionMultiplexer</c>
    /// ya registrado en el contenedor (se resuelve de forma diferida); dejar en false si el servicio
    /// no usa Redis.
    /// </param>
    public static IHostApplicationBuilder AddRappixObservability(
        this IHostApplicationBuilder builder,
        string serviceName,
        bool instrumentRedis = false)
    {
        string environment = builder.Environment.EnvironmentName;

        // Las llamadas HTTP del propio sink de Seq no deben generar spans (telemetria sobre telemetria).
        // El exportador OTLP del SDK ya se auto-excluye via SuppressInstrumentationScope, pero el sink
        // OTLP de Serilog (UseRappixLogging) no: se excluye su endpoint en HttpClient y sus spans gRPC
        // (LogsService/Export) con OtlpExportFilterProcessor.
        string? seqHost = GetHost(builder.Configuration["Seq:ServerUrl"]);
        string? otlpAuthority = GetAuthority(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: serviceName, serviceVersion: ServiceVersion)
                .AddAttributes([new KeyValuePair<string, object>("deployment.environment", environment)]))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(serviceName)
                    .AddSource("MassTransit")
                    .AddSource("Npgsql")
                    .AddAspNetCoreInstrumentation(options =>
                        options.Filter = httpContext => !httpContext.Request.Path.StartsWithSegments("/health"))
                    .AddHttpClientInstrumentation(options =>
                        options.FilterHttpRequestMessage = request =>
                            !string.Equals(request.RequestUri?.Host, seqHost, StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(request.RequestUri?.Authority, otlpAuthority, StringComparison.OrdinalIgnoreCase))
                    .AddGrpcClientInstrumentation(options => options.SuppressDownstreamInstrumentation = true)
                    // Antes del exporter: los processors corren en orden de registro en OnEnd.
                    .AddProcessor(new OtlpExportFilterProcessor())
                    .AddOtlpExporter();

                if (instrumentRedis)
                {
                    tracing.AddRedisInstrumentation();
                }
            })
            .WithMetrics(metrics => metrics
                .AddMeter(serviceName)
                .AddMeter("Npgsql")
                .AddMeter("MassTransit")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter());

        return builder;
    }

    private static string? GetHost(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ? uri.Host : null;

    private static string? GetAuthority(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ? uri.Authority : null;
}
