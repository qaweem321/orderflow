using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OrderFlow.BuildingBlocks.Errors;
using OrderFlow.BuildingBlocks.Web;

namespace Gateway.Api.Identity;

public sealed record RegisterRequest(
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(80, MinimumLength = 2)] string DisplayName,
    [Required, StringLength(128, MinimumLength = 10)] string Password);

public sealed record LoginRequest([Required, StringLength(254)] string Email, [Required, StringLength(128)] string Password);

public static class AuthEndpoints
{
    public const string LoginRateLimit = "login";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Auth");
        auth.MapPost("/register", RegisterAsync).AllowAnonymous().RequireRateLimiting(LoginRateLimit)
            .WithSummary("Create a customer account and sign in.");
        auth.MapPost("/login", LoginAsync).AllowAnonymous().RequireRateLimiting(LoginRateLimit)
            .WithSummary("Sign in. Returns a JWT for the Authorization: Bearer header.");
        auth.MapGet("/me", MeAsync).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request, IdentityDbContext db, IPasswordHasher<User> hasher, TokenService tokens, TimeProvider clock, CancellationToken ct)
    {
        var user = new User(request.Email, request.DisplayName, Roles.Customer, clock.GetUtcNow());
        user.SetPasswordHash(hasher.HashPassword(user, request.Password));
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException("email_taken", "An account with this email already exists. Sign in instead.");
        }

        return Results.Ok(tokens.Issue(user));
    }

    private static async Task<TokenResponse> LoginAsync(
        LoginRequest request, IdentityDbContext db, IPasswordHasher<User> hasher, TokenService tokens, CancellationToken ct)
    {
        var email = User.Normalize(request.Email);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email, ct);

        // Hash even for unknown emails, so response time doesn't reveal which emails have accounts.
        var hash = user?.PasswordHash ?? DummyHash.Value;
        var result = hasher.VerifyHashedPassword(user ?? DummyUser, hash, request.Password);
        if (user is null || result == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedException();
        }

        return tokens.Issue(user);
    }

    private static async Task<UserDto> MeAsync(ClaimsPrincipal principal, IdentityDbContext db, CancellationToken ct)
    {
        var id = Guid.Parse(principal.UserId());
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ct) ?? throw new NotFoundException("User");
        return UserDto.From(user);
    }

    private static readonly User DummyUser = new("dummy@invalid", "dummy", Roles.Customer, DateTimeOffset.UnixEpoch);
    private static readonly Lazy<string> DummyHash = new(() => new PasswordHasher<User>().HashPassword(DummyUser, Guid.NewGuid().ToString()));
}

public sealed class UnauthorizedException() : AppException(401, "invalid_credentials", "Email or password is wrong.");
