using FluentValidation;
using JobTrack.Application.Common;
using JobTrack.Domain.Entities;
using JobTrack.Domain.Enums;

namespace JobTrack.Application.Applications;

/// <summary>Card/list representation. <see cref="AllowedTransitions"/> lets clients offer only valid moves.</summary>
public sealed record ApplicationSummaryDto(
    Guid Id,
    Guid JobOfferId,
    string JobTitle,
    Guid CompanyId,
    string CompanyName,
    string? Location,
    WorkMode WorkMode,
    ApplicationStatus Status,
    DateTimeOffset StatusChangedAt,
    DateOnly? AppliedOn,
    string? NextAction,
    DateOnly? FollowUpOn,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public IReadOnlyList<ApplicationStatus> AllowedTransitions => ApplicationStatusRules.AllowedTransitionsFrom(Status);
}

public sealed record ApplicationStatusHistoryDto(
    Guid Id,
    ApplicationStatus? FromStatus,
    ApplicationStatus ToStatus,
    DateTimeOffset ChangedAt,
    string? Note);

public sealed record ApplicationDetailDto(
    ApplicationSummaryDto Summary,
    Guid? ResumeId,
    string? ResumeFileName,
    string? CoverLetter,
    ApplicationSource? Source,
    string? Notes,
    string? RejectionReason,
    IReadOnlyList<ApplicationStatusHistoryDto> History);

public sealed record CreateApplicationRequest(
    Guid JobOfferId,
    ApplicationStatus Status,
    DateOnly? AppliedOn,
    Guid? ResumeId,
    string? CoverLetter,
    ApplicationSource? Source,
    string? Notes,
    string? NextAction,
    DateOnly? FollowUpOn);

/// <summary>Editable fields. The status is deliberately absent: it only changes through the status endpoint.</summary>
public sealed record UpdateApplicationRequest(
    DateOnly? AppliedOn,
    Guid? ResumeId,
    string? CoverLetter,
    ApplicationSource? Source,
    string? Notes,
    string? NextAction,
    DateOnly? FollowUpOn);

public sealed record ChangeApplicationStatusRequest(ApplicationStatus Status, string? Note, string? RejectionReason);

public sealed class ApplicationQuery : PagedQuery
{
    public string? Search { get; init; }
    public IReadOnlyList<ApplicationStatus>? Status { get; init; }
    public Guid? CompanyId { get; init; }

    /// <summary>Inclusive date range on the application date (or the creation date when it has not been sent yet).</summary>
    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    /// <summary>"updated" (default), "appliedOn", "company" or "followUp".</summary>
    public string? Sort { get; init; }

    public bool Desc { get; init; }
}

/// <summary>Filters for the Kanban board, which loads every matching card at once (capped).</summary>
public sealed class ApplicationBoardQuery
{
    public const int MaxCards = 500;

    public string? Search { get; init; }
    public Guid? CompanyId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

public sealed class CreateApplicationRequestValidator : AbstractValidator<CreateApplicationRequest>
{
    public CreateApplicationRequestValidator()
    {
        RuleFor(r => r.JobOfferId).NotEmpty();
        RuleFor(r => r.Status)
            .IsInEnum()
            .Must(ApplicationStatusRules.IsValidInitialStatus)
            .WithMessage("Una postulación nueva no puede comenzar en un estado cerrado.");
        RuleFor(r => r.CoverLetter).MaximumLength(JobApplication.MaxCoverLetterLength);
        RuleFor(r => r.Notes).MaximumLength(JobApplication.MaxNotesLength);
        RuleFor(r => r.NextAction).MaximumLength(JobApplication.MaxNextActionLength);
        RuleFor(r => r.Source).IsInEnum();
    }
}

public sealed class UpdateApplicationRequestValidator : AbstractValidator<UpdateApplicationRequest>
{
    public UpdateApplicationRequestValidator()
    {
        RuleFor(r => r.CoverLetter).MaximumLength(JobApplication.MaxCoverLetterLength);
        RuleFor(r => r.Notes).MaximumLength(JobApplication.MaxNotesLength);
        RuleFor(r => r.NextAction).MaximumLength(JobApplication.MaxNextActionLength);
        RuleFor(r => r.Source).IsInEnum();
    }
}

public sealed class ChangeApplicationStatusRequestValidator : AbstractValidator<ChangeApplicationStatusRequest>
{
    public ChangeApplicationStatusRequestValidator()
    {
        RuleFor(r => r.Status).IsInEnum();
        RuleFor(r => r.Note).MaximumLength(ApplicationStatusHistory.MaxNoteLength);
        RuleFor(r => r.RejectionReason).MaximumLength(JobApplication.MaxRejectionReasonLength);
    }
}

public sealed class ApplicationQueryValidator : AbstractValidator<ApplicationQuery>
{
    public ApplicationQueryValidator()
    {
        this.ApplyPaging();
        RuleFor(q => q.Search).MaximumLength(200);
        RuleForEach(q => q.Status).IsInEnum();
        RuleFor(q => q.To).GreaterThanOrEqualTo(q => q.From).When(q => q.From is not null && q.To is not null)
            .WithMessage("La fecha final debe ser posterior a la inicial.");
        RuleFor(q => q.Sort)
            .Must(s => s is null or "updated" or "appliedOn" or "company" or "followUp")
            .WithMessage("Orden no válido. Valores permitidos: updated, appliedOn, company, followUp.");
    }
}

public sealed class ApplicationBoardQueryValidator : AbstractValidator<ApplicationBoardQuery>
{
    public ApplicationBoardQueryValidator()
    {
        RuleFor(q => q.Search).MaximumLength(200);
        RuleFor(q => q.To).GreaterThanOrEqualTo(q => q.From).When(q => q.From is not null && q.To is not null)
            .WithMessage("La fecha final debe ser posterior a la inicial.");
    }
}
