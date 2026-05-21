using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Identity.Application.Authentication;
using Rappix.Identity.Domain.Users;

namespace Rappix.Identity.Infrastructure.Authentication;

/// <summary>
/// Emite access tokens JWT firmados con HS256 y genera/hashea los tokens opacos
/// (refresh y confirmacion de email).
/// </summary>
internal sealed class JwtTokenService : ITokenService
{
    private const int MinimumKeyLengthBytes = 32; // 256 bits para HS256

    private readonly JwtOptions _options;
    private readonly IDateTimeProvider _clock;
    private readonly SigningCredentials _signingCredentials;
    private readonly JsonWebTokenHandler _handler = new();

    public JwtTokenService(IOptions<JwtOptions> options, IDateTimeProvider clock)
    {
        _options = options.Value;
        _clock = clock;

        if (Encoding.UTF8.GetByteCount(_options.SigningKey) < MinimumKeyLengthBytes)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey debe tener al menos 256 bits (32 bytes). Configure una clave mas larga.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public AccessToken CreateAccessToken(User user)
    {
        DateTime issuedAt = _clock.UtcNow;
        DateTime expires = issuedAt.AddMinutes(_options.AccessTokenLifetimeMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expires,
            SigningCredentials = _signingCredentials,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = user.Id.Value.ToString(),
                ["email"] = user.Email.Value,
                ["userType"] = user.UserType.ToString(),
                ["email_confirmed"] = user.EmailConfirmed,
                ["jti"] = Guid.CreateVersion7().ToString(),
            },
        };

        return new AccessToken(_handler.CreateToken(descriptor), expires);
    }

    public string GenerateOpaqueToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    public string ComputeHash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
