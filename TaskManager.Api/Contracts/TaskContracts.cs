using System.ComponentModel.DataAnnotations;
using TaskManager.Api.Models;

namespace TaskManager.Api.Contracts;

public class TaskWriteRequest
{
    // [Required] also rejects whitespace-only strings (AllowEmptyStrings is false by default).
    // Enum fields are validated by JsonStringEnumConverter during deserialization.
    [Required, StringLength(200)]
    public string Title { get; init; } = "";

    [StringLength(2000)]
    public string? Description { get; init; }

    public TaskItemStatus Status { get; init; } = TaskItemStatus.Pending;

    public TaskPriority Priority { get; init; } = TaskPriority.Medium;

    public DateOnly? DueDate { get; init; }
}

public record TaskResponse(
    Guid Id,
    string Title,
    string? Description,
    TaskItemStatus Status,
    TaskPriority Priority,
    DateOnly? DueDate,
    DateTime? CompletedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record TaskStatsResponse(int Open, int DueToday, int Overdue, int Done);

public record ClearCompletedResponse(int Deleted);

public enum TaskSortField
{
    CreatedAt,
    DueDate,
    Priority,
    Title,
}

public enum SortDirection
{
    Asc,
    Desc,
}
