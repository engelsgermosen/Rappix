using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace Rappix.Gateway.Tests.Integration;

/// <summary>
/// Verifica el cableado de WebSockets para el hub de Tracking. La cobertura real es el smoke
/// E2E manual (Paso 14): el TestServer in-process de WebApplicationFactory no soporta nativamente
/// el upgrade a WebSocket por LongPolling/SignalR — por eso el test de conexion real es
/// <c>SkippableFact</c> opt-in via env <c>GATEWAY_E2E_WS</c>.
/// </summary>
public sealed class WebSocketsTests : IClassFixture<GatewayApiFactory>
{
    private readonly GatewayApiFactory factory;

    public WebSocketsTests(GatewayApiFactory factory)
    {
        this.factory = factory;
        this.factory.ResetAll();
    }

    [Fact]
    public async Task Hub_path_is_anonymous_route_and_passes_to_tracking_cluster()
    {
        // Sanity check del cableado: la ruta /hubs/tracking/* tiene AuthorizationPolicy=Anonymous,
        // por lo que un GET sin token NO devuelve 401 del gateway y SI llega al WireMock de Tracking.
        // (El upgrade real a WebSocket es lo que cubre el smoke manual.)
        factory.Tracking.Given(Request.Create().WithPath("/hubs/tracking/negotiate").UsingPost())
                        .RespondWith(Response.Create().WithStatusCode(200).WithBody("ok"));

        HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await client.PostAsync("/hubs/tracking/negotiate?negotiateVersion=1", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.Tracking.LogEntries.Should().ContainSingle()
            .Which.RequestMessage!.Path.Should().Be("/hubs/tracking/negotiate");
    }

    [SkippableFact]
    public async Task Hub_handshake_via_signalr_client_succeeds_when_E2E_enabled()
    {
        // Opt-in: solo corre si GATEWAY_E2E_WS=1. El TestServer in-process de WebApplicationFactory
        // por defecto no enruta WebSockets — el smoke real es manual con Docker compose up.
        Skip.If(
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GATEWAY_E2E_WS")),
            "Set GATEWAY_E2E_WS=1 and run gateway + tracking en Docker compose para validar conexion real al hub.");

        string jwt = GatewayApiFactory.CreateJwt();
        HubConnection connection = new HubConnectionBuilder()
            .WithUrl($"{factory.Server.BaseAddress}hubs/tracking?access_token={jwt}", opts =>
            {
                opts.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

        await connection.StartAsync();
        connection.State.Should().Be(HubConnectionState.Connected);
        await connection.DisposeAsync();
    }
}
