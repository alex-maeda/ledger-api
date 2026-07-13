using System.Net;
using System.Net.Http.Json;

namespace TaskManager.Api.Tests;

/// <summary>The API must reject bad input with a 400 and a structured error body.</summary>
public class ValidationTests(TestWebAppFactory factory) : IClassFixture<TestWebAppFactory>
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Create_WithMissingOrBlankTitle_Returns400(string? title)
    {
        var client = await factory.CreateUserClientAsync();

        var response = await client.PostAsJsonAsync("/api/tasks", new { title }, TestSupport.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await client.ListTasksAsync());
    }

    [Fact]
    public async Task Create_WithTitleOver200Chars_Returns400()
    {
        var client = await factory.CreateUserClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/tasks", new { title = new string('x', 201) }, TestSupport.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithMalformedDueDate_Returns400()
    {
        var client = await factory.CreateUserClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/tasks", new { title = "Valid", dueDate = "not-a-date" }, TestSupport.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithUnknownPriority_Returns400()
    {
        var client = await factory.CreateUserClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/tasks", new { title = "Valid", priority = "urgent" }, TestSupport.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithNumericStatus_Returns400()
    {
        var client = await factory.CreateUserClientAsync();

        // Enums are string-only in this API; raw integers (including undefined ones
        // like 99) must not be accepted.
        var response = await client.PostAsJsonAsync(
            "/api/tasks", new { title = "Valid", status = 99 }, TestSupport.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithBlankTitle_Returns400_AndTaskIsUnchanged()
    {
        var client = await factory.CreateUserClientAsync();
        var task = await client.CreateTaskAsync(new { title = "Before" });

        var response = await client.PutAsJsonAsync(
            $"/api/tasks/{task.Id}", new { title = "  " }, TestSupport.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Before", Assert.Single(await client.ListTasksAsync()).Title);
    }

    [Fact]
    public async Task Stats_WithMissingOrMalformedToday_Returns400()
    {
        var client = await factory.CreateUserClientAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/tasks/stats")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/tasks/stats?today=12/07/2026")).StatusCode);
    }

    [Theory]
    [InlineData("not-an-email", "password-123")]
    [InlineData("valid@example.com", "short")]
    [InlineData("", "password-123")]
    public async Task Register_WithInvalidInput_Returns400(string email, string password)
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register", new { email, password }, TestSupport.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
