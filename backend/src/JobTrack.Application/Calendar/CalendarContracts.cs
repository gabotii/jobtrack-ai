using FluentValidation;
using JobTrack.Application.Common;
using JobTrack.Domain.Entities;
using JobTrack.Domain.Enums;

namespace JobTrack.Application.Calendar;

/// <summary>
/// <see cref="StartsAt"/>/<see cref="EndsAt"/> are UTC instants (ISO 8601 with offset). <see cref="TimeZone"/> is the
/// IANA zone the event was scheduled in, so clients can show "10:00 (Europe/Madrid)" regardless of the viewer's zone.
/// </summary>
public sealed record CalendarEventDto(
    Guid Id,
    string Title,
    CalendarEventType Type,
    CalendarEventStatus Status,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string TimeZone,
    string? Location,
    string? MeetingUrl,
    string? Notes,
    Guid? CompanyId,
    string? CompanyName,
    Guid? ApplicationId,
    string? ApplicationJobTitle,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Create/update body. If <see cref="ApplicationId"/> is given, the company is taken from the application and a
/// different <see cref="CompanyId"/> is rejected.
/// </summary>
public sealed record SaveCalendarEventRequest(
    string Title,
    CalendarEventType Type,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string TimeZone,
    string? Location,
    string? MeetingUrl,
    string? Notes,
    Guid? CompanyId,
    Guid? ApplicationId);

public sealed record ChangeCalendarEventStatusRequest(CalendarEventStatus Status);

public sealed class CalendarEventQuery
{
    public const int MaxRangeDays = 366;

    public DateTimeOffset From { get; init; }
    public DateTimeOffset To { get; init; }
    public Guid? ApplicationId { get; init; }
    public Guid? CompanyId { get; init; }
    public bool IncludeCancelled { get; init; } = true;
}

public sealed class SaveCalendarEventRequestValidator : AbstractValidator<SaveCalendarEventRequest>
{
    public static readonly TimeSpan MaxDuration = TimeSpan.FromDays(7);

    public SaveCalendarEventRequestValidator()
    {
        RuleFor(r => r.Title).NotEmpty().MaximumLength(CalendarEvent.MaxTitleLength);
        RuleFor(r => r.Type).IsInEnum();
        RuleFor(r => r.StartsAt).NotEmpty();
        RuleFor(r => r.EndsAt)
            .GreaterThan(r => r.StartsAt)
            .WithMessage("La hora de finalización debe ser posterior a la de inicio.");
        RuleFor(r => r.EndsAt)
            .Must((r, end) => end - r.StartsAt <= MaxDuration)
            .WithMessage("Un evento no puede durar más de 7 días.");
        RuleFor(r => r.TimeZone).IanaTimeZone();
        RuleFor(r => r.Location).MaximumLength(CalendarEvent.MaxLocationLength);
        RuleFor(r => r.MeetingUrl).HttpUrl(CalendarEvent.MaxUrlLength);
        RuleFor(r => r.Notes).MaximumLength(CalendarEvent.MaxNotesLength);
    }
}

public sealed class ChangeCalendarEventStatusRequestValidator : AbstractValidator<ChangeCalendarEventStatusRequest>
{
    public ChangeCalendarEventStatusRequestValidator() => RuleFor(r => r.Status).IsInEnum();
}

public sealed class CalendarEventQueryValidator : AbstractValidator<CalendarEventQuery>
{
    public CalendarEventQueryValidator()
    {
        RuleFor(q => q.From).NotEmpty();
        RuleFor(q => q.To).NotEmpty().GreaterThan(q => q.From).WithMessage("El final del rango debe ser posterior al inicio.");
        RuleFor(q => q.To)
            .Must((q, to) => to - q.From <= TimeSpan.FromDays(CalendarEventQuery.MaxRangeDays))
            .WithMessage($"El rango consultado no puede superar {CalendarEventQuery.MaxRangeDays} días.");
    }
}
