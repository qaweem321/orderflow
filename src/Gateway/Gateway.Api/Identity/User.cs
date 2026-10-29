namespace Gateway.Api.Identity;

public class User
{
    private User() { }

    public User(string email, string displayName, string role, DateTimeOffset now)
    {
        Email = Normalize(email);
        DisplayName = displayName.Trim();
        Role = role;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = "";
    public string DisplayName { get; private set; } = "";
    public string PasswordHash { get; private set; } = "";
    public string Role { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }

    public void SetPasswordHash(string hash) => PasswordHash = hash;

    /// <summary>Emails are compared in lower case everywhere.</summary>
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
