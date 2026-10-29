using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Gateway.Api.Identity;
using OrderFlow.BuildingBlocks.Web;

namespace OrderFlow.IntegrationTests;

public class GatewayTests(Backend stack)
{
    [Theory]
    [InlineData("admin@orderflow.test", "orderflow-admin", Roles.Admin)]
    [InlineData("CUSTOMER@orderflow.test", "orderflow-customer", Roles.Customer)]
    public async Task Demo_users_can_sign_in(string email, string password, string role)
    {
        var response = await stack.Gateway.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password });
        var token = await Backend.ReadAsync<TokenResponse>(response);

        Assert.Equal(role, token.User.Role);
        Assert.True(token.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData("admin@orderflow.test", "wrong-password")]
    [InlineData("nobody@orderflow.test", "orderflow-admin")]
    public async Task Wrong_credentials_get_the_same_401(string email, string password)
    {
        var response = await stack.Gateway.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("invalid_credentials", await Backend.ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Registering_signs_in_as_a_customer_and_emails_are_unique()
    {
        var http = stack.Gateway.CreateClient();
        var email = $"{Guid.NewGuid():N}@Example.com";

        var token = await Backend.ReadAsync<TokenResponse>(await http.PostAsJsonAsync("/api/auth/register",
            new { email, displayName = "New Customer", password = "a-long-password" }));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        var me = await http.GetFromJsonAsync<UserDto>("/api/auth/me", Backend.Json);
        var again = await stack.Gateway.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { email = email.ToUpperInvariant(), displayName = "Copy", password = "a-long-password" });

        Assert.Equal(Roles.Customer, token.User.Role);
        Assert.Equal(email.ToLowerInvariant(), me!.Email);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Short_passwords_are_refused() =>
        Assert.Equal(HttpStatusCode.BadRequest, (await stack.Gateway.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { email = $"{Guid.NewGuid():N}@example.com", displayName = "Short", password = "short" })).StatusCode);

    [Fact]
    public async Task Gateway_tokens_are_accepted_by_the_services()
    {
        var login = await Backend.ReadAsync<TokenResponse>(await stack.Gateway.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = "customer@orderflow.test", password = "orderflow-customer" }));
        var ordering = stack.Ordering.CreateClient();
        ordering.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);

        Assert.Equal(HttpStatusCode.OK, (await ordering.GetAsync("/api/orders")).StatusCode);
    }
}
