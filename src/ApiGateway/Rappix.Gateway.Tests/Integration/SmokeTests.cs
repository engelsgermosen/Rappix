using System.Net;
using FluentAssertions;

namespace Rappix.Gateway.Tests.Integration;

/// <summary>Smoke trivial: el gateway arranca y la landing HTML responde.</summary>
public sealed class SmokeTests : IClassFixture<GatewayApiFactory>
{
    private readonly GatewayApiFactory factory;

    public SmokeTests(GatewayApiFactory factory) => this.factory = factory;

    [Fact]
    public async Task Root_returns_200_with_landing_html()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Rappix API Gateway");
    }
}
