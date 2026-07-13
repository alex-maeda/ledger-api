using System.Net;
using System.Net.Http.Json;
using TaskManager.Api.Contracts;

namespace TaskManager.Api.Tests;

/// <summary>
/// The highest-risk area of this app: user A must never be able to read, modify,
/// or delete user B's tasks. Cross-user access returns 404 (not 403) so the API
/// does not reveal which task IDs exist.
/// </summary>
public class OwnershipTests(TestWebAppFactory factory) : IClassFixture<TestWebAppFactory>
{
    [Fact]
    public async Task Get_TaskOwnedByAnotherUser_Returns404()
    {
        var owner = await factory.CreateUserClientAsync();
        var intruder = await factory.CreateUserClientAsync();
        var task = await owner.CreateTaskAsync(new { title = "Owner's task" });

        var response = await intruder.GetAsync($"/api/tasks/{task.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_TaskOwnedByAnotherUser_Returns404_AndLeavesTaskUnchanged()
    {
        var owner = await factory.CreateUserClientAsync();
        var intruder = await factory.CreateUserClientAsync();
        var task = await owner.CreateTaskAsync(new { title = "Original title" });

        var response = await intruder.PutAsJsonAsync(
            $"/api/tasks/{task.Id}", new { title = "Hijacked" }, TestSupport.Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var stillMine = await owner.ListTasksAsync();
        Assert.Equal("Original title", Assert.Single(stillMine).Title);
    }

    [Fact]
    public async Task Delete_TaskOwnedByAnotherUser_Returns404_AndTaskSurvives()
    {
        var owner = await factory.CreateUserClientAsync();
        var intruder = await factory.CreateUserClientAsync();
        var task = await owner.CreateTaskAsync(new { title = "Keep me" });

        var response = await intruder.DeleteAsync($"/api/tasks/{task.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Single(await owner.ListTasksAsync());
    }

    [Fact]
    public async Task List_ReturnsOnlyOwnTasks()
    {
        var userA = await factory.CreateUserClientAsync();
        var userB = await factory.CreateUserClientAsync();
        await userA.CreateTaskAsync(new { title = "A's task" });
        await userB.CreateTaskAsync(new { title = "B's task" });

        var tasksSeenByA = await userA.ListTasksAsync();

        Assert.Equal("A's task", Assert.Single(tasksSeenByA).Title);
    }

    [Fact]
    public async Task ClearCompleted_DoesNotTouchOtherUsersCompletedTasks()
    {
        var userA = await factory.CreateUserClientAsync();
        var userB = await factory.CreateUserClientAsync();
        await userA.CreateTaskAsync(new { title = "A done", status = "done" });
        await userB.CreateTaskAsync(new { title = "B done", status = "done" });

        var response = await userA.DeleteAsync("/api/tasks/completed");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ClearCompletedResponse>(TestSupport.Json);

        Assert.Equal(1, result!.Deleted);
        Assert.Empty(await userA.ListTasksAsync());
        Assert.Single(await userB.ListTasksAsync());
    }

    [Fact]
    public async Task Stats_CountOnlyOwnTasks()
    {
        var userA = await factory.CreateUserClientAsync();
        var userB = await factory.CreateUserClientAsync();
        await userA.CreateTaskAsync(new { title = "A's only task" });
        await userB.CreateTaskAsync(new { title = "B 1" });
        await userB.CreateTaskAsync(new { title = "B 2" });

        var response = await userA.GetAsync("/api/tasks/stats?today=2026-07-12");
        response.EnsureSuccessStatusCode();
        var stats = await response.Content.ReadFromJsonAsync<TaskStatsResponse>(TestSupport.Json);

        Assert.Equal(1, stats!.Open);
    }
}
