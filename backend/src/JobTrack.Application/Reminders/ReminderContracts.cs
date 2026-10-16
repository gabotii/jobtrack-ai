using FluentValidation;
using JobTrack.Application.Common;
using JobTrack.Domain.Entities;
using JobTrack.Domain.Enums;

namespace JobTrack.Application.Reminders;

public sealed record ReminderDto(
    Guid Id,
    string Title,
    string? Description,
    DateTimeOffset DueAt,
    ReminderPriority Priority,
    bool IsCompleted,
    DateTimeOffset? CompletedAt,
    Guid? ApplicationId,
    string? ApplicationJobTitle,
    string? CompanyName,
    Guid? CalendarEventId,
    string? CalendarEventTitle,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SaveReminderRequest(
    string Title,
    string? Description,
    DateTimeOffset DueAt,
    ReminderPriority Priority,
    Guid? ApplicationId,
    Guid? CalendarEventId);

public enum ReminderStatusFilter
{
    Pending = 0,
    Completed = 1,
    All = 2,
}

public sealed class ReminderQuery : PagedQuery
{
    public ReminderStatusFilter Status { get; init; } = ReminderStatusFilter.Pending;
    public Guid? ApplicationId { get; init; }

    /// <summary>Only reminders due before this instant (e.g. "due today or overdue").</summary>
    public DateTimeOffset? DueBefore { get; init; }
}

public sealed class SaveReminderRequestValidator : AbstractValidator<SaveReminderRequest>
{
    public SaveReminderRequestValidator()
    {
        RuleFor(r => r.Title).NotEmpty().MaximumLength(Reminder.MaxTitleLength);
        RuleFor(r => r.Description).MaximumLength(Reminder.MaxDescriptionLength);
        RuleFor(r => r.DueAt).NotEmpty();
        RuleFor(r => r.Priority).IsInEnum();
    }
}

public sealed class ReminderQueryValidator : AbstractValidator<ReminderQuery>
{
    public ReminderQueryValidator()
    {
        this.ApplyPaging();
        RuleFor(q => q.Status).IsInEnum();
    }
}
