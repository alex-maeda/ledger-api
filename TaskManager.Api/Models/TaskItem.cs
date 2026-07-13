namespace TaskManager.Api.Models;

public enum TaskItemStatus
{
    Pending = 0,
    InProgress = 1,
    Done = 2,
}

public enum TaskPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
}

public class TaskItem
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Pending;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    // A due date is a calendar date, not an instant: DateOnly avoids all timezone conversion.
    public DateOnly? DueDate { get; set; }

    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
