using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TaskManager.Api.Contracts;

namespace TaskManager.Api.Tests;

public static class TestSupport
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Registers a fresh user and returns an HttpClient authenticated as them.</summary>
    public static async Task<HttpClient> CreateUserClientAsync(this TestWebAppFactory factory, string? email = null)
    {
        var client = factory.CreateClient();
        email ??= $"user-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync(
            "/api/auth/register", new { email, password = "password-123" }, Json);
        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    public static async Task<TaskResponse> CreateTaskAsync(this HttpClient client, object payload)
    {
        var response = await client.PostAsJsonAsync("/api/tasks", payload, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskResponse>(Json))!;
    }

    public static async Task<List<TaskResponse>> ListTasksAsync(this HttpClient client, string query = "")
    {
        var response = await client.GetAsync($"/api/tasks{query}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<TaskResponse>>(Json))!;
    }
}
