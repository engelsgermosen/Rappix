using System.Net;
using System.Text.Json;
using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Rappix.Gateway.Tests.Integration;

/// <summary>
/// Verifica el aggregator /health (estado de los 9 downstream) y el /health/live (liveness propio).
/// Con failureStatus=Degraded, un downstream caido NO tira el gateway a 503 — devuelve 200 con
/// el entry marcado como Degraded. Esto evita que orchestrators reinicien el gateway por un
/// downstream caido (ver ADR-0011 §3.4).
/// </summary>
public sealed class HealthAggregationTests : IClassFixture<GatewayApiFactory>
{
    private readonly GatewayApiFactory factory;

    public HealthAggregationTests(GatewayApiFactory factory)
    {
        this.factory = factory;
        this.factory.ResetAll();
    }

    [Fact]
    public async Task Health_live_returns_200_without_touching_downstreams()
    {
        HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // NINGUN downstream debe haber sido invocado.
        factory.Identity.LogEntries.Should().BeEmpty();
        factory.Tracking.LogEntries.Should().BeEmpty();
        factory.Notifications.LogEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task Health_aggregated_returns_200_when_all_9_downstreams_are_healthy()
    {
        StubHealthOk(factory.Identity);
        StubHealthOk(factory.Merchants);
        StubHealthOk(factory.Catalog);
        StubHealthOk(factory.Pricing);
        StubHealthOk(factory.Orders);
        StubHealthOk(factory.Dispatch);
        StubHealthOk(factory.Tracking);
        StubHealthOk(factory.Payments);
        StubHealthOk(factory.Notifications);

        HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
        doc.RootElement.GetProperty("entries").EnumerateObject().Should().HaveCount(9);
    }

    [Fact]
    public async Task Health_aggregated_returns_200_with_degraded_when_one_downstream_is_down()
    {
        StubHealthOk(factory.Identity);
        StubHealthOk(factory.Merchants);
        StubHealthOk(factory.Catalog);
        StubHealthOk(factory.Pricing);
        StubHealthOk(factory.Orders);
        StubHealthOk(factory.Dispatch);
        StubHealthOk(factory.Tracking);
        StubHealthOk(factory.Payments);
        // Notifications: 503 — el aggregator lo marca Degraded, pero /health responde 200.
        factory.Notifications.Given(Request.Create().WithPath("/health").UsingGet())
                              .RespondWith(Response.Create().WithStatusCode(503));

        HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);   // gateway NO cae
        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("status").GetString().Should().Be("Degraded");
        doc.RootElement.GetProperty("entries").GetProperty("notifications")
            .GetProperty("status").GetString().Should().Be("Degraded");
    }

    private static void StubHealthOk(WireMockServer server)
    {
        server.Given(Request.Create().WithPath("/health").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody("""{"status":"ok"}"""));
    }
}
