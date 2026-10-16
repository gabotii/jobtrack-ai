using JobTrack.Domain.Enums;

namespace JobTrack.Domain.Entities;

/// <summary>Immutable record of a status change. <see cref="FromStatus"/> is null for the entry created with the application.</summary>
public sealed class ApplicationStatusHistory
{
    public const int MaxNoteLength = 1_000;

    private ApplicationStatusHistory()
    {
    }

    internal ApplicationStatusHistory(
        Guid applicationId,
        ApplicationStatus? fromStatus,
        ApplicationStatus toStatus,
        DateTimeOffset changedAt,
        string? note)
    {
        ApplicationId = applicationId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ChangedAt = changedAt;
        Note = note;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid ApplicationId { get; private set; }
    public JobApplication? Application { get; private set; }
    public ApplicationStatus? FromStatus { get; private set; }
    public ApplicationStatus ToStatus { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }
    public string? Note { get; private set; }
}
