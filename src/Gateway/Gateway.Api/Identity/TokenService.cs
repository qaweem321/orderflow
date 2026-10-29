using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OrderFlow.BuildingBlocks.Web;

namespace Gateway.Api.Identity;

public sealed record UserDto(Guid Id, string Email, string DisplayName, string Role)
{
    public static UserDto From(User u) => new(u.Id, u.Email, u.DisplayName, u.Role);
}

public sealed record TokenResponse(string Token, DateTimeOffset ExpiresAt, UserDto User);

/// <summary>Issues the HMAC-signed JWT that every service validates with the shared key.</summary>
public sealed class TokenService(IOptions<JwtOptions> options, TimeProvider clock)
{
    private readonly JsonWebTokenHandler _handler = new();

    public TokenResponse Issue(User user)
    {
        var jwt = options.Value;
        var now = clock.GetUtcNow();
        var expires = now + jwt.Lifetime;
        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(jwt.Key(), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", user.Id.ToString()),
                new Claim("email", user.Email),
                new Claim("name", user.DisplayName),
                new Claim("role", user.Role),
            ]),
        });
        return new TokenResponse(token, expires, UserDto.From(user));
    }
}
