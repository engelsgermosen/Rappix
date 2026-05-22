using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Rappix.Pricing.Tests.Integration;

/// <summary>Crea JWTs de prueba firmados con la misma clave que valida la API (HS256).</summary>
internal static class TestTokens
{
    public const string SigningKey = "test-signing-key-rappix-pricing-0123456789-abcdefghij";
    public const string Issuer = "https://localhost:5001";
    public const string Audience = "rappix";

    public static string Customer(Guid userId) => Create(userId, "Customer");

    public static string Admin(Guid userId) => Create(userId, "Admin");

    private static string Create(Guid userId, string userType)
    {
        var handler = new JsonWebTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Expires = DateTime.UtcNow.AddMinutes(30),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = userId.ToString(),
                ["userType"] = userType,
                ["email_confirmed"] = "true",
            },
        };

        return handler.CreateToken(descriptor);
    }
}
