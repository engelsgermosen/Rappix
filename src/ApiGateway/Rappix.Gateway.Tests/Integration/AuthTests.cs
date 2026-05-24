using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Rappix.Gateway.Tests.Integration;

/// <summary>
/// Defensa en profundidad de auth en el gateway:
/// 1. Rutas Anonymous (login, register, webhooks, public GETs, hub) pasan sin token.
/// 2. Rutas protegidas SIN token devuelven 401 EN EL GATEWAY (no llegan al downstream).
/// 3. Rutas protegidas con token valido pasan; el Authorization header se propaga.
/// 4. Token firmado con otra key devuelve 401 EN EL GATEWAY.
/// 5. El gateway NO enforce roles — un Customer puede llegar a /admin/* (el downstream filtra).
/// </summary>
public sealed class AuthTests : IClassFixture<GatewayApiFactory>
{
    private readonly GatewayApiFactory factory;
    private readonly HttpClient client;

    public AuthTests(GatewayApiFactory factory)
    {
        this.factory = factory;
        this.factory.ResetAll();
        StubOkOnAny(factory.Identity,  "identity");
        StubOkOnAny(factory.Orders,    "orders");
        StubOkOnAny(factory.Payments,  "payments");
        StubOkOnAny(factory.Merchants, "merchants");
        client = factory.CreateClient();
    }

    private static void StubOkOnAny(WireMockServer server, string body)
    {
        server.Given(Request.Create().WithPath("/*").UsingAnyMethod())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody(body));
    }

    [Fact]
    public async Task Login_anonymous_route_passes_without_token()
    {
        HttpResponseMessage response = await client.PostAsync(
            "/api/v1/auth/login",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.Identity.LogEntries.Should().ContainSingle();
    }

    [Fact]
    public async Task Protected_route_without_token_returns_401_at_gateway()
    {
        HttpResponseMessage response = await client.GetAsync("/api/v1/orders/" + Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        // Critico: el WireMock de Orders NO recibe la request — el gateway corta.
        factory.Orders.LogEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task Protected_route_with_valid_token_reaches_downstream_with_authorization_header()
    {
        string jwt = GatewayApiFactory.CreateJwt();
        HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/orders/" + Guid.NewGuid());
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.Orders.LogEntries.Should().ContainSingle()
            .Which.RequestMessage!.Headers!["Authorization"]
            .Should().Contain($"Bearer {jwt}");
    }

    [Fact]
    public async Task Protected_route_with_jwt_signed_by_other_key_returns_401_at_gateway()
    {
        string jwt = GatewayApiFactory.CreateJwt(
            signingKey: "other-key-not-recognized-by-gateway-32bytes-min",
            userId: Guid.NewGuid());
        HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/orders/" + Guid.NewGuid());
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        factory.Orders.LogEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task Stripe_webhook_anonymous_passes_without_token()
    {
        HttpResponseMessage response = await client.PostAsync(
            "/payments/webhooks/stripe",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.Payments.LogEntries.Should().ContainSingle();
    }

    [Fact]
    public async Task Gateway_does_not_enforce_roles_admin_reachable_with_customer_jwt()
    {
        // El gateway solo enforza autenticacion — el role check ("RequireAdmin") vive en Merchants.
        // Aqui un Customer llega al endpoint admin del gateway; el downstream (real, no este mock)
        // devolveria 403. El gateway debe pasar la request, no cortarla.
        string jwt = GatewayApiFactory.CreateJwt(userType: "Customer");
        HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/admin/merchants/X");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.Merchants.LogEntries.Should().ContainSingle();
    }
}
