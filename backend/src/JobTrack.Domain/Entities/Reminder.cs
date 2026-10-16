using JobTrack.Domain.Common;
using JobTrack.Domain.Enums;

namespace JobTrack.Domain.Entities;

/// <summary>A to-do with a due instant, optionally linked to an application and/or a calendar event.</summary>
public sealed class Reminder : OwnedEntity
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 5_000;

    private Reminder()
    {
    }

    public Reminder(Guid userId, string title, DateTimeOffset dueAt, ReminderPriority priority)
    {
        UserId = userId;
        Title = title;
        DueAt = dueAt.ToUniversalTime();
        Priority = priority;
    }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset DueAt { get; private set; }
    public ReminderPriority Priority { get; set; }
    public bool IsCompleted { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public Guid? ApplicationId { get; set; }
    public JobApplication? Application { get; private set; }
    public Guid? CalendarEventId { get; set; }
    public CalendarEvent? CalendarEvent { get; private set; }

    public void Reschedule(DateTimeOffset dueAt) => DueAt = dueAt.ToUniversalTime();

    public void Complete(DateTimeOffset now)
    {
        if (IsCompleted)
        {
            return;
        }

        IsCompleted = true;
        CompletedAt = now;
    }

    public void Reopen()
    {
        IsCompleted = false;
        CompletedAt = null;
    }
}
