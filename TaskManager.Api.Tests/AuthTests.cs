using System.Net;
using System.Net.Http.Json;
using TaskManager.Api.Contracts;

namespace TaskManager.Api.Tests;

public class AuthTests(TestWebAppFactory factory) : IClassFixture<TestWebAppFactory>
{
    [Fact]
    public async Task Register_ThenLogin_ReturnsWorkingToken()
    {
        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync(
            "/api/auth/register", new { email, password = "password-123" }, TestSupport.Json);
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var login = await client.PostAsJsonAsync(
            "/api/auth/login", new { email, password = "password-123" }, TestSupport.Json);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>(TestSupport.Json);
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth!.Token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/tasks")).StatusCode);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409_CaseInsensitively()
    {
        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";

        var first = await client.PostAsJsonAsync(
            "/api/auth/register", new { email, password = "password-123" }, TestSupport.Json);
        first.EnsureSuccessStatusCode();

        var second = await client.PostAsJsonAsync(
            "/api/auth/register", new { email = email.ToUpperInvariant(), password = "password-123" },
            TestSupport.Json);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";
        await factory.CreateUserClientAsync(email);

        var login = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new { email, password = "wrong-password" }, TestSupport.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task TaskEndpoints_WithoutToken_Return401()
    {
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tasks")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/tasks", new { title = "x" }, TestSupport.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.DeleteAsync($"/api/tasks/{Guid.NewGuid()}")).StatusCode);
    }
}
