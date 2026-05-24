using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Rappix.Gateway.Tests.Integration;

/// <summary>
/// Verifica las dos politicas de rate limit del gateway (anonimo por IP / autenticado por sub)
/// y la exencion del webhook Stripe. Cada test usa una particion DISTINTA (IP anon, sub auth,
/// NoLimiter para stripe) — esto las hace independientes entre si y no necesitan reset de window.
/// </summary>
public sealed class RateLimitTests : IClassFixture<RateLimitedGatewayApiFactory>
{
    private readonly RateLimitedGatewayApiFactory factory;
    private readonly HttpClient client;

    public RateLimitTests(RateLimitedGatewayApiFactory factory)
    {
        this.factory = factory;
        this.factory.ResetAll();
        StubOkOnAny(factory.Merchants, "merchants");
        StubOkOnAny(factory.Orders,    "orders");
        StubOkOnAny(factory.Payments,  "payments");
        client = factory.CreateClient();
    }

    private static void StubOkOnAny(WireMockServer server, string body)
    {
        server.Given(Request.Create().WithPath("/*").UsingAnyMethod())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody(body));
    }

    [Fact]
    public async Task Anonymous_requests_exceeding_limit_return_429_with_retry_after()
    {
        // Particion IP anonima: PermitLimit=3. Los 3 primeros pasan; el 4to devuelve 429.
        for (int i = 0; i < RateLimitedGatewayApiFactory.AnonymousPermitLimit; i++)
        {
            HttpResponseMessage ok = await client.GetAsync("/api/v1/merchants/nearby");
            ok.StatusCode.Should().Be(HttpStatusCode.OK, $"request {i + 1} debia pasar");
        }

        HttpResponseMessage rejected = await client.GetAsync("/api/v1/merchants/nearby");

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        rejected.Headers.RetryAfter.Should().NotBeNull("el OnRejected setea Retry-After con los segundos restantes");
    }

    [Fact]
    public async Task Authenticated_requests_exceeding_limit_return_429_for_same_user()
    {
        // Particion u:{sub} con sliding window: PermitLimit=5. Los 5 primeros pasan; el 6to devuelve 429.
        // sub fijo (mismo userId) por toda la rafaga — particion compartida.
        Guid userId = Guid.NewGuid();
        string jwt = GatewayApiFactory.CreateJwt(userId);

        for (int i = 0; i < RateLimitedGatewayApiFactory.AuthenticatedPermitLimit; i++)
        {
            HttpRequestMessage req = new(HttpMethod.Get, "/api/v1/orders/" + Guid.NewGuid());
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            HttpResponseMessage ok = await client.SendAsync(req);
            ok.StatusCode.Should().Be(HttpStatusCode.OK, $"request {i + 1} debia pasar");
        }

        HttpRequestMessage rejectedReq = new(HttpMethod.Get, "/api/v1/orders/" + Guid.NewGuid());
        rejectedReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        HttpResponseMessage rejected = await client.SendAsync(rejectedReq);

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Stripe_webhook_is_exempt_from_rate_limiting()
    {
        // El webhook de Stripe esta en la particion NoLimiter — un atacante puede mandar 100 sin disparar 429.
        // (La autenticidad de la llamada se valida con la firma Stripe, no con rate limit.)
        const int excessive = 20;
        for (int i = 0; i < excessive; i++)
        {
            HttpResponseMessage response = await client.PostAsync(
                "/payments/webhooks/stripe",
                new StringContent("{}", Encoding.UTF8, "application/json"));
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"request {i + 1} no debe ser limitado");
        }
    }
}
