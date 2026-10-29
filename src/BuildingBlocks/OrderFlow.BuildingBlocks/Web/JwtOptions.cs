using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace OrderFlow.BuildingBlocks.Web;

/// <summary>Shared by the gateway (which issues tokens) and every service (which validates them).</summary>
public sealed class JwtOptions
{
    public const string Section = "Jwt";

    [Required]
    public string Issuer { get; set; } = "orderflow";

    [Required]
    public string Audience { get; set; } = "orderflow";

    /// <summary>HMAC-SHA256 key, at least 32 characters. Generate: <c>openssl rand -base64 48</c>.</summary>
    [Required, MinLength(32)]
    public string SigningKey { get; set; } = "";

    [Range(typeof(TimeSpan), "00:05:00", "1.00:00:00")]
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromHours(8);

    public SymmetricSecurityKey Key() => new(Encoding.UTF8.GetBytes(SigningKey));
}

public static class Roles
{
    public const string Admin = "admin";
    public const string Customer = "customer";
}
