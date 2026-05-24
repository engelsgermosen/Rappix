using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Rappix.Gateway.Api.Health;

/// <summary>
/// Health check que pingea <c>/health</c> de un cluster YARP, leyendo el address del
/// <see cref="IConfiguration"/> en RUNTIME (no en startup). Esto permite tests con
/// <c>WebApplicationFactory</c> override del destino via in-memory configuration sin tener
/// que recurrir a env vars en static ctor.
/// </summary>
public sealed class DownstreamUrlHealthCheck : IHealthCheck
{
    private readonly string clusterName;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IConfiguration configuration;

    public DownstreamUrlHealthCheck(string clusterName, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        this.clusterName = clusterName;
        this.httpClientFactory = httpClientFactory;
        this.configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        string? baseUrl = configuration.GetValue<string>(
            $"ReverseProxy:Clusters:{clusterName}:Destinations:default:Address");
        if (string.IsNullOrEmpty(baseUrl))
        {
            return HealthCheckResult.Degraded($"Cluster {clusterName}: sin address en config.");
        }

        try
        {
            HttpClient client = httpClientFactory.CreateClient(nameof(DownstreamUrlHealthCheck));
            client.Timeout = TimeSpan.FromSeconds(5);
            HttpResponseMessage response = await client.GetAsync(
                new Uri(new Uri(baseUrl), "/health"),
                cancellationToken).ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy($"{clusterName}: {(int)response.StatusCode}")
                : HealthCheckResult.Degraded($"{clusterName}: {(int)response.StatusCode}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return HealthCheckResult.Degraded($"{clusterName}: {ex.GetType().Name} ({ex.Message})");
        }
    }
}
