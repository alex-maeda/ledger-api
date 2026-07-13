using System.Net;
using System.Net.Http.Json;
using TaskManager.Api.Contracts;
using TaskManager.Api.Models;

namespace TaskManager.Api.Tests;

/// <summary>Behavior of the task CRUD cycle, filters, stats, and completion tracking.</summary>
public class TaskFlowTests(TestWebAppFactory factory) : IClassFixture<TestWebAppFactory>
{
    [Fact]
    public async Task Create_Update_Delete_RoundTrip()
    {
        var client = await factory.CreateUserClientAsync();

        var created = await client.CreateTaskAsync(new
        {
            title = "  Buy milk  ",
            description = "2 liters",
            priority = "high",
            dueDate = "2026-08-01",
        });
        Assert.Equal("Buy milk", created.Title); // whitespace trimmed
        Assert.Equal(TaskPriority.High, created.Priority);
        Assert.Equal(new DateOnly(2026, 8, 1), created.DueDate);

        var update = await client.PutAsJsonAsync($"/api/tasks/{created.Id}", new
        {
            title = "Buy oat milk",
            status = "inProgress",
            priority = "medium",
            dueDate = "2026-08-02",
        }, TestSupport.Json);
        update.EnsureSuccessStatusCode();
        var updated = await update.Content.ReadFromJsonAsync<TaskResponse>(TestSupport.Json);
        Assert.Equal("Buy oat milk", updated!.Title);
        Assert.Equal(TaskItemStatus.InProgress, updated.Status);
        Assert.Null(updated.Description); // PUT is a full replace of editable fields

        var delete = await client.DeleteAsync($"/api/tasks/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Empty(await client.ListTasksAsync());
    }

    [Fact]
    public async Task MarkingDone_SetsCompletedAt_AndReopeningClearsIt()
    {
        var client = await factory.CreateUserClientAsync();
        var task = await client.CreateTaskAsync(new { title = "Finish report" });
        Assert.Null(task.CompletedAt);

        var done = await (await client.PutAsJsonAsync($"/api/tasks/{task.Id}",
                new { title = "Finish report", status = "done" }, TestSupport.Json))
            .Content.ReadFromJsonAsync<TaskResponse>(TestSupport.Json);
        Assert.NotNull(done!.CompletedAt);

        var reopened = await (await client.PutAsJsonAsync($"/api/tasks/{task.Id}",
                new { title = "Finish report", status = "pending" }, TestSupport.Json))
            .Content.ReadFromJsonAsync<TaskResponse>(TestSupport.Json);
        Assert.Null(reopened!.CompletedAt);
    }

    [Fact]
    public async Task List_FiltersByStatusAndSearch()
    {
        var client = await factory.CreateUserClientAsync();
        await client.CreateTaskAsync(new { title = "Write report", status = "done" });
        await client.CreateTaskAsync(new { title = "Review REPORT draft" });
        await client.CreateTaskAsync(new { title = "Book flights" });

        var doneOnly = await client.ListTasksAsync("?status=done");
        Assert.Equal("Write report", Assert.Single(doneOnly).Title);

        var search = await client.ListTasksAsync("?search=report");
        Assert.Equal(2, search.Count); // case-insensitive

        var combined = await client.ListTasksAsync("?search=report&status=pending");
        Assert.Equal("Review REPORT draft", Assert.Single(combined).Title);
    }

    [Fact]
    public async Task List_SortsByDueDate_WithUndatedTasksLast()
    {
        var client = await factory.CreateUserClientAsync();
        await client.CreateTaskAsync(new { title = "No date" });
        await client.CreateTaskAsync(new { title = "Later", dueDate = "2026-09-01" });
        await client.CreateTaskAsync(new { title = "Sooner", dueDate = "2026-07-15" });

        var tasks = await client.ListTasksAsync("?sortBy=dueDate&sortDir=asc");

        Assert.Equal(["Sooner", "Later", "No date"], tasks.Select(t => t.Title).ToArray());
    }

    [Fact]
    public async Task List_WithInvalidFilterOrSortValues_Returns400()
    {
        var client = await factory.CreateUserClientAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/tasks?sortBy=banana")).StatusCode);
        // Undefined numeric enum values must be rejected by model binding, not silently filtered.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/tasks?status=99")).StatusCode);
    }

    [Fact]
    public async Task Stats_AreComputedRelativeToClientProvidedDate()
    {
        var client = await factory.CreateUserClientAsync();
        await client.CreateTaskAsync(new { title = "Overdue", dueDate = "2026-07-10" });
        await client.CreateTaskAsync(new { title = "Due today", dueDate = "2026-07-12" });
        await client.CreateTaskAsync(new { title = "Future", dueDate = "2026-07-20" });
        await client.CreateTaskAsync(new { title = "Done", status = "done", dueDate = "2026-07-01" });

        var response = await client.GetAsync("/api/tasks/stats?today=2026-07-12");
        response.EnsureSuccessStatusCode();
        var stats = await response.Content.ReadFromJsonAsync<TaskStatsResponse>(TestSupport.Json);

        Assert.Equal(new TaskStatsResponse(Open: 3, DueToday: 1, Overdue: 1, Done: 1), stats);
    }

    [Fact]
    public async Task ClearCompleted_RemovesOnlyDoneTasks()
    {
        var client = await factory.CreateUserClientAsync();
        await client.CreateTaskAsync(new { title = "Done 1", status = "done" });
        await client.CreateTaskAsync(new { title = "Done 2", status = "done" });
        await client.CreateTaskAsync(new { title = "Still open" });

        var response = await client.DeleteAsync("/api/tasks/completed");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ClearCompletedResponse>(TestSupport.Json);

        Assert.Equal(2, result!.Deleted);
        Assert.Equal("Still open", Assert.Single(await client.ListTasksAsync()).Title);
    }
}
