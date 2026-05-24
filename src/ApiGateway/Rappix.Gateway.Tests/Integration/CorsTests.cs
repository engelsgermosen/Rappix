using System.Net;
using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Rappix.Gateway.Tests.Integration;

/// <summary>
/// Verifica la policy ProductionCors (la que aplica en Testing, igual que en prod): orígenes
/// permitidos desde <c>Cors:AllowedOrigins</c> (default <c>http://localhost:3000</c>), credentials
/// y headers/metodos restringidos.
/// </summary>
public sealed class CorsTests : IClassFixture<GatewayApiFactory>
{
    private readonly GatewayApiFactory factory;
    private readonly HttpClient client;

    public CorsTests(GatewayApiFactory factory)
    {
        this.factory = factory;
        this.factory.ResetAll();
        factory.Merchants.Given(Request.Create().WithPath("/*").UsingAnyMethod())
                          .RespondWith(Response.Create().WithStatusCode(200));
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Preflight_from_allowed_origin_returns_204_with_allow_origin_header()
    {
        HttpRequestMessage preflight = new(HttpMethod.Options, "/api/v1/merchants/nearby");
        preflight.Headers.Add("Origin", "http://localhost:3000");
        preflight.Headers.Add("Access-Control-Request-Method", "GET");
        preflight.Headers.Add("Access-Control-Request-Headers", "Authorization");

        HttpResponseMessage response = await client.SendAsync(preflight);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin")
            .Should().ContainSingle().Which.Should().Be("http://localhost:3000");
        response.Headers.GetValues("Access-Control-Allow-Credentials")
            .Should().ContainSingle().Which.Should().Be("true");
    }

    [Fact]
    public async Task Preflight_from_disallowed_origin_does_not_emit_allow_origin_header()
    {
        HttpRequestMessage preflight = new(HttpMethod.Options, "/api/v1/merchants/nearby");
        preflight.Headers.Add("Origin", "http://evil.example.com");
        preflight.Headers.Add("Access-Control-Request-Method", "GET");

        HttpResponseMessage response = await client.SendAsync(preflight);

        // CORS middleware solo emite Allow-Origin para orígenes permitidos.
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task Request_from_allowed_origin_carries_allow_origin_in_response()
    {
        HttpRequestMessage req = new(HttpMethod.Get, "/api/v1/merchants/nearby");
        req.Headers.Add("Origin", "http://localhost:3000");

        HttpResponseMessage response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("Access-Control-Allow-Origin")
            .Should().ContainSingle().Which.Should().Be("http://localhost:3000");
    }
}
