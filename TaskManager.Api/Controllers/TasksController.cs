using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Contracts;
using TaskManager.Api.Data;
using TaskManager.Api.Models;

namespace TaskManager.Api.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize]
public class TasksController(AppDbContext db) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<List<TaskResponse>>> List(
        [FromQuery] TaskItemStatus? status,
        [FromQuery] TaskPriority? priority,
        [FromQuery] string? search,
        [FromQuery] TaskSortField sortBy = TaskSortField.CreatedAt,
        [FromQuery] SortDirection sortDir = SortDirection.Desc)
    {
        var query = db.Tasks.AsNoTracking().Where(t => t.UserId == UserId);

        if (status is not null)
            query = query.Where(t => t.Status == status);
        if (priority is not null)
            query = query.Where(t => t.Priority == priority);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(t =>
                t.Title.ToLower().Contains(term) ||
                (t.Description != null && t.Description.ToLower().Contains(term)));
        }

        query = ApplySort(query, sortBy, sortDir);

        var tasks = await query.ToListAsync();
        return Ok(tasks.Select(ToResponse).ToList());
    }

    [HttpGet("stats")]
    public async Task<ActionResult<TaskStatsResponse>> Stats([FromQuery(Name = "today")] string? todayRaw)
    {
        // The client supplies its local calendar date so "overdue" and "due today" are
        // computed in the user's timezone, not the server's.
        if (!DateOnly.TryParseExact(todayRaw, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var today))
        {
            ModelState.AddModelError("today", "The 'today' query parameter is required in yyyy-MM-dd format.");
            return ValidationProblem(ModelState);
        }

        var tasks = await db.Tasks.AsNoTracking()
            .Where(t => t.UserId == UserId)
            .Select(t => new { t.Status, t.DueDate })
            .ToListAsync();

        return Ok(new TaskStatsResponse(
            Open: tasks.Count(t => t.Status != TaskItemStatus.Done),
            DueToday: tasks.Count(t => t.Status != TaskItemStatus.Done && t.DueDate == today),
            Overdue: tasks.Count(t => t.Status != TaskItemStatus.Done && t.DueDate < today),
            Done: tasks.Count(t => t.Status == TaskItemStatus.Done)));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TaskResponse>> Get(Guid id)
    {
        var task = await db.Tasks.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == id && t.UserId == UserId);

        return task is null ? TaskNotFound() : Ok(ToResponse(task));
    }

    [HttpPost]
    public async Task<ActionResult<TaskResponse>> Create(TaskWriteRequest request)
    {
        var now = DateTime.UtcNow;
        var task = new TaskItem { Id = Guid.NewGuid(), UserId = UserId, CreatedAt = now };
        ApplyWrite(task, request, now);

        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = task.Id }, ToResponse(task));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TaskResponse>> Update(Guid id, TaskWriteRequest request)
    {
        var task = await db.Tasks.SingleOrDefaultAsync(t => t.Id == id && t.UserId == UserId);
        if (task is null)
            return TaskNotFound();

        ApplyWrite(task, request, DateTime.UtcNow);
        await db.SaveChangesAsync();
        return Ok(ToResponse(task));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await db.Tasks
            .Where(t => t.Id == id && t.UserId == UserId)
            .ExecuteDeleteAsync();

        return deleted == 0 ? TaskNotFound() : NoContent();
    }

    [HttpDelete("completed")]
    public async Task<ActionResult<ClearCompletedResponse>> ClearCompleted()
    {
        var deleted = await db.Tasks
            .Where(t => t.UserId == UserId && t.Status == TaskItemStatus.Done)
            .ExecuteDeleteAsync();

        return Ok(new ClearCompletedResponse(deleted));
    }

    // 404 (not 403) for tasks that exist but belong to someone else, so the API
    // does not leak which task IDs exist.
    private ObjectResult TaskNotFound() =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Task not found.");

    // Single place that maps a write request onto the entity - including the
    // Done→CompletedAt transition, which applies to both create and update.
    private static void ApplyWrite(TaskItem task, TaskWriteRequest request, DateTime now)
    {
        if (request.Status == TaskItemStatus.Done && task.Status != TaskItemStatus.Done)
            task.CompletedAt = now;
        else if (request.Status != TaskItemStatus.Done)
            task.CompletedAt = null;

        task.Title = request.Title.Trim();
        task.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        task.Status = request.Status;
        task.Priority = request.Priority;
        task.DueDate = request.DueDate;
        task.UpdatedAt = now;
    }

    private static IQueryable<TaskItem> ApplySort(
        IQueryable<TaskItem> query, TaskSortField sortBy, SortDirection sortDir)
    {
        var asc = sortDir == SortDirection.Asc;
        return sortBy switch
        {
            // Tasks without a due date sort last either way; DateOnly maps to ISO text
            // in SQLite, so text ordering is chronological.
            TaskSortField.DueDate => asc
                ? query.OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate)
                    .ThenByDescending(t => t.CreatedAt)
                : query.OrderBy(t => t.DueDate == null).ThenByDescending(t => t.DueDate)
                    .ThenByDescending(t => t.CreatedAt),
            TaskSortField.Priority => asc
                ? query.OrderBy(t => t.Priority).ThenByDescending(t => t.CreatedAt)
                : query.OrderByDescending(t => t.Priority).ThenByDescending(t => t.CreatedAt),
            TaskSortField.Title => asc
                ? query.OrderBy(t => t.Title.ToLower())
                : query.OrderByDescending(t => t.Title.ToLower()),
            _ => asc
                ? query.OrderBy(t => t.CreatedAt)
                : query.OrderByDescending(t => t.CreatedAt),
        };
    }

    private static TaskResponse ToResponse(TaskItem t) => new(
        t.Id, t.Title, t.Description, t.Status, t.Priority,
        t.DueDate, t.CompletedAt, t.CreatedAt, t.UpdatedAt);
}
