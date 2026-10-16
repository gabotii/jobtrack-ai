using JobTrack.Domain.Common;
using JobTrack.Domain.Enums;

namespace JobTrack.Domain.Entities;

/// <summary>
/// A user's application to a <see cref="JobOffer"/>. There is at most one application per offer.
/// The status may only change through <see cref="ChangeStatus"/>, which enforces
/// <see cref="ApplicationStatusRules"/> and records the history.
/// </summary>
public sealed class JobApplication : OwnedEntity
{
    public const int MaxCoverLetterLength = 20_000;
    public const int MaxNotesLength = 10_000;
    public const int MaxNextActionLength = 300;
    public const int MaxRejectionReasonLength = 1_000;

    private readonly List<ApplicationStatusHistory> _statusHistory = [];

    private JobApplication()
    {
    }

    public Guid JobOfferId { get; private set; }
    public JobOffer? JobOffer { get; private set; }

    public ApplicationStatus Status { get; private set; }
    public DateTimeOffset StatusChangedAt { get; private set; }

    /// <summary>Calendar date (in the user's time zone) on which the application was sent.</summary>
    public DateOnly? AppliedOn { get; set; }

    public Guid? ResumeId { get; set; }
    public Resume? Resume { get; private set; }

    public string? CoverLetter { get; set; }
    public ApplicationSource? Source { get; set; }
    public string? Notes { get; set; }
    public string? NextAction { get; set; }
    public DateOnly? FollowUpOn { get; set; }
    public string? RejectionReason { get; private set; }

    public IReadOnlyCollection<ApplicationStatusHistory> StatusHistory => _statusHistory;

    public static JobApplication Create(
        Guid userId,
        Guid jobOfferId,
        ApplicationStatus initialStatus,
        DateTimeOffset now,
        DateOnly today)
    {
        if (!ApplicationStatusRules.IsValidInitialStatus(initialStatus))
        {
            throw new DomainException(
                "application.invalid_initial_status",
                "Una postulación nueva no puede comenzar en un estado cerrado.");
        }

        var application = new JobApplication
        {
            UserId = userId,
            JobOfferId = jobOfferId,
            Status = initialStatus,
            StatusChangedAt = now,
        };
        application.EnsureAppliedOn(today);
        application._statusHistory.Add(
            new ApplicationStatusHistory(application.Id, fromStatus: null, initialStatus, now, note: null));
        return application;
    }

    /// <summary>Applies a transition and returns the new history entry (also added to <see cref="StatusHistory"/>).</summary>
    public ApplicationStatusHistory ChangeStatus(
        ApplicationStatus newStatus,
        DateTimeOffset now,
        DateOnly today,
        string? note,
        string? rejectionReason)
    {
        if (newStatus == Status)
        {
            throw new DomainException("application.status_unchanged", "La postulación ya se encuentra en ese estado.");
        }

        if (!ApplicationStatusRules.CanTransition(Status, newStatus))
        {
            throw new DomainException(
                "application.invalid_transition",
                $"No se puede pasar de «{ApplicationStatusRules.Label(Status)}» a «{ApplicationStatusRules.Label(newStatus)}».");
        }

        if (rejectionReason is not null && newStatus != ApplicationStatus.Rejected)
        {
            throw new DomainException(
                "application.rejection_reason_not_allowed",
                "El motivo de rechazo solo puede indicarse al marcar la postulación como rechazada.");
        }

        var previous = Status;
        Status = newStatus;
        StatusChangedAt = now;
        RejectionReason = newStatus == ApplicationStatus.Rejected ? rejectionReason : null;
        EnsureAppliedOn(today);
        var entry = new ApplicationStatusHistory(Id, previous, newStatus, now, note);
        _statusHistory.Add(entry);
        return entry;
    }

    /// <summary>Applications that reach the "sent" stages get an application date if the user did not set one.</summary>
    private void EnsureAppliedOn(DateOnly today)
    {
        if (AppliedOn is null && ApplicationStatusRules.IsInProgress(Status))
        {
            AppliedOn = today;
        }
    }
}
