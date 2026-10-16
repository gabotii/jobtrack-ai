using JobTrack.Domain.Common;
using JobTrack.Domain.Enums;

namespace JobTrack.Domain.Entities;

/// <summary>
/// A scheduled event (interview, call, follow-up…). Start and end are stored as UTC instants; <see cref="TimeZone"/>
/// keeps the IANA zone the event was scheduled in so it can be shown in its original local time.
/// An event may be linked to an application; in that case its company is always the application's company.
/// </summary>
public sealed class CalendarEvent : OwnedEntity
{
    public const int MaxTitleLength = 200;
    public const int MaxLocationLength = 300;
    public const int MaxUrlLength = 2_048;
    public const int MaxNotesLength = 10_000;
    public const int MaxTimeZoneLength = 64;

    private CalendarEvent()
    {
    }

    public CalendarEvent(Guid userId, string title, CalendarEventType type)
    {
        UserId = userId;
        Title = title;
        Type = type;
        Status = CalendarEventStatus.Scheduled;
    }

    public string Title { get; set; } = string.Empty;
    public CalendarEventType Type { get; set; }
    public CalendarEventStatus Status { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public string TimeZone { get; private set; } = "UTC";
    public string? Location { get; set; }
    public string? MeetingUrl { get; set; }
    public string? Notes { get; set; }

    public Guid? CompanyId { get; private set; }
    public Company? Company { get; private set; }
    public Guid? ApplicationId { get; private set; }
    public JobApplication? Application { get; private set; }

    public void Schedule(DateTimeOffset startsAt, DateTimeOffset endsAt, string timeZone)
    {
        if (endsAt <= startsAt)
        {
            throw new DomainException("event.invalid_range", "La hora de finalización debe ser posterior a la de inicio.");
        }

        StartsAt = startsAt.ToUniversalTime();
        EndsAt = endsAt.ToUniversalTime();
        TimeZone = timeZone;
    }

    /// <summary>Links the event to an application (which implies its company) or directly to a company.</summary>
    public void LinkTo(Guid? applicationId, Guid? companyId)
    {
        ApplicationId = applicationId;
        CompanyId = companyId;
    }

    public void SetStatus(CalendarEventStatus status) => Status = status;
}
