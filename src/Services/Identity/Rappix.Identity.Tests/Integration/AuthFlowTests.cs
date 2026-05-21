using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using Rappix.Identity.Application.Authentication;

namespace Rappix.Identity.Tests.Integration;

/// <summary>Pruebas de integracion del flujo de autenticacion contra PostgreSQL real (Testcontainers).</summary>
public sealed class AuthFlowTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task FullFlow_Register_Confirm_Login_Me_Refresh_Logout_Works()
    {
        HttpClient client = factory.CreateClient();
        string email = UniqueEmail();
        const string password = "Sup3rSecret!";

        // Registro
        HttpResponseMessage register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            phoneNumber = (string?)null,
            password,
            firstName = "Engels",
            lastName = "Germosen",
        });
        register.StatusCode.Should().Be(HttpStatusCode.OK);

        // Confirmacion de email (se captura el enlace del mock de SendGrid)
        string confirmationUrl = CapturedConfirmationUrl();
        (string userId, string token) = ParseConfirmation(confirmationUrl);
        HttpResponseMessage confirm = await client.GetAsync($"/api/v1/auth/confirm-email?userId={userId}&token={Uri.EscapeDataString(token)}");
        confirm.StatusCode.Should().Be(HttpStatusCode.OK);

        // Login
        AuthResult login = await LoginAsync(client, email, password);
        login.AccessToken.Should().NotBeNullOrEmpty();
        login.RefreshToken.Should().NotBeNullOrEmpty();

        // /me con el JWT
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        HttpResponseMessage meResponse = await client.GetAsync("/api/v1/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        UserResult me = (await meResponse.Content.ReadFromJsonAsync<UserResult>(JsonOptions))!;
        me.Email.Should().Be(email);
        me.EmailConfirmed.Should().BeTrue();

        // Refresh (rotacion)
        AuthResult refreshed = await RefreshAsync(client, login.RefreshToken);
        refreshed.RefreshToken.Should().NotBe(login.RefreshToken);

        // Logout
        HttpResponseMessage logout = await client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = refreshed.RefreshToken });
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RefreshTokenReuse_RevokesEntireChain()
    {
        HttpClient client = factory.CreateClient();
        string email = UniqueEmail();
        const string password = "An0therSecret!";

        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            phoneNumber = (string?)null,
            password,
            firstName = "Ada",
            lastName = "Lovelace",
        });

        AuthResult login = await LoginAsync(client, email, password);

        // Primera rotacion: refresh1 -> refresh2 (refresh1 queda revocado)
        AuthResult rotated = await RefreshAsync(client, login.RefreshToken);

        // Reuso del refresh1 ya revocado: deteccion de robo -> 401 y revocacion de toda la cadena
        HttpResponseMessage reuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = login.RefreshToken });
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // refresh2 tambien quedo revocado por la deteccion de robo
        HttpResponseMessage afterReuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = rotated.RefreshToken });
        afterReuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        HttpClient client = factory.CreateClient();
        string email = UniqueEmail();

        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            phoneNumber = (string?)null,
            password = "CorrectHorse1",
            firstName = "Grace",
            lastName = "Hopper",
        });

        HttpResponseMessage login = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = email, password = "WrongPassword1" });
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_AsMerchant_CreatesUserWithMerchantType()
    {
        HttpClient client = factory.CreateClient();
        string email = UniqueEmail();

        HttpResponseMessage register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            phoneNumber = (string?)null,
            password = "Merch4ntPass!",
            firstName = "Meri",
            lastName = "Comercio",
            accountType = "Merchant",
        });

        register.StatusCode.Should().Be(HttpStatusCode.OK);
        UserResult created = (await register.Content.ReadFromJsonAsync<UserResult>(JsonOptions))!;
        created.UserType.Should().Be("Merchant");
    }

    [Fact]
    public async Task Me_WithoutToken_ReturnsUnauthorized()
    {
        HttpClient client = factory.CreateClient();
        HttpResponseMessage me = await client.GetAsync("/api/v1/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<AuthResult> LoginAsync(HttpClient client, string email, string password)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = email, password });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AuthResult>(JsonOptions))!;
    }

    private async Task<AuthResult> RefreshAsync(HttpClient client, string refreshToken)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AuthResult>(JsonOptions))!;
    }

    private string CapturedConfirmationUrl()
    {
        object?[] arguments = factory.EmailSender.ReceivedCalls()
            .Last(call => call.GetMethodInfo().Name == nameof(IEmailSender.SendEmailConfirmationAsync))
            .GetArguments();
        return (string)arguments[2]!;
    }

    private static (string UserId, string Token) ParseConfirmation(string url)
    {
        var uri = new Uri(url);
        Dictionary<string, string> parameters = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(parts => parts[0], parts => Uri.UnescapeDataString(parts[1]));
        return (parameters["userId"], parameters["token"]);
    }

    private static string UniqueEmail() => $"user-{Guid.CreateVersion7():N}@rappix.test";

    private sealed record AuthResult(string AccessToken, DateTime AccessTokenExpiresAtUtc, string RefreshToken, UserResult User);

    private sealed record UserResult(
        Guid Id,
        string Email,
        string? PhoneNumber,
        string FirstName,
        string LastName,
        string UserType,
        bool EmailConfirmed,
        bool PhoneConfirmed,
        DateTime CreatedAtUtc);
}
