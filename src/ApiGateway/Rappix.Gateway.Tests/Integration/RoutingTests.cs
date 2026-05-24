using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Rappix.Gateway.Tests.Integration;

/// <summary>
/// Verifica que YARP rutea correctamente al cluster esperado para los 15 routes (9 clusters).
/// El detalle critico: <c>/api/v1/admin/merchants/*</c> debe ir a Merchants y
/// <c>/api/v1/admin/pricing/*</c> a Pricing (paths disjuntos, sin <c>Order</c> explicito).
/// Los <c>/me</c> de Identity y Catalog tienen <c>Order=-10</c> para matchear antes de las
/// rutas Anonymous mas amplias.
/// </summary>
public sealed class RoutingTests : IClassFixture<GatewayApiFactory>
{
    private readonly GatewayApiFactory factory;
    private readonly HttpClient client;

    public RoutingTests(GatewayApiFactory factory)
    {
        this.factory = factory;
        this.factory.ResetAll();
        // Cada cluster responde 200 con un cuerpo identificable; los tests verifican que llego al correcto.
        StubOkOnAny(factory.Identity,      "identity");
        StubOkOnAny(factory.Merchants,     "merchants");
        StubOkOnAny(factory.Catalog,       "catalog");
        StubOkOnAny(factory.Pricing,       "pricing");
        StubOkOnAny(factory.Orders,        "orders");
        StubOkOnAny(factory.Dispatch,      "dispatch");
        StubOkOnAny(factory.Tracking,      "tracking");
        StubOkOnAny(factory.Payments,      "payments");
        StubOkOnAny(factory.Notifications, "notifications");
        client = factory.CreateClient();
        // Token Bearer en todas las requests: las rutas Anonymous lo ignoran; las protegidas lo aceptan.
        // Asi este test verifica SOLO routing (la cobertura de auth vive en AuthTests).
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GatewayApiFactory.CreateJwt());
    }

    private static void StubOkOnAny(WireMockServer server, string body)
    {
        server.Given(Request.Create().WithPath("/*").UsingAnyMethod())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody(body));
    }

    [Theory]
    [InlineData("/api/v1/auth/login",            "identity")]
    [InlineData("/api/v1/auth/me",               "identity")]   // override mas especifico (Order=-10)
    [InlineData("/api/v1/users/X",               "identity")]
    [InlineData("/api/v1/merchants/nearby",      "merchants")]
    [InlineData("/api/v1/admin/merchants/X",     "merchants")]  // disjunto vs admin/pricing
    [InlineData("/api/v1/catalog/items/search",  "catalog")]
    [InlineData("/api/v1/catalog/me/items",      "catalog")]    // override (Order=-10)
    [InlineData("/api/v1/pricing/quote",         "pricing")]
    [InlineData("/api/v1/admin/pricing/coupons", "pricing")]    // disjunto vs admin/merchants
    [InlineData("/api/v1/orders/X",              "orders")]
    [InlineData("/api/v1/couriers/me",           "dispatch")]
    [InlineData("/api/v1/tracking/orders/X",     "tracking")]
    public async Task Path_routes_to_expected_cluster(string path, string expectedCluster)
    {
        HttpResponseMessage response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be(expectedCluster);
    }

    [Fact]
    public async Task Stripe_webhook_routes_to_payments_with_post()
    {
        HttpResponseMessage response = await client.PostAsync(
            "/payments/webhooks/stripe",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("payments");
    }

    [Fact]
    public async Task Admin_merchants_does_not_reach_pricing_cluster()
    {
        await client.GetAsync("/api/v1/admin/merchants/X");

        factory.Pricing.LogEntries.Should().BeEmpty();
        factory.Merchants.LogEntries.Should().ContainSingle();
    }

    [Fact]
    public async Task Admin_pricing_does_not_reach_merchants_cluster()
    {
        await client.GetAsync("/api/v1/admin/pricing/coupons");

        factory.Merchants.LogEntries.Should().BeEmpty();
        factory.Pricing.LogEntries.Should().ContainSingle();
    }
}
