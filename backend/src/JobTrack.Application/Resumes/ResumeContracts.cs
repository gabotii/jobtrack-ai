using FluentValidation;
using JobTrack.Domain.Entities;

namespace JobTrack.Application.Resumes;

public sealed record ResumeDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    int CharacterCount,
    int AnalysisCount,
    DateTimeOffset CreatedAt);

public sealed record ResumeDetailDto(ResumeDto Resume, string ExtractedText);

/// <summary>An upload as received by the API, already size-limited by the web layer.</summary>
public sealed record ResumeUpload(string FileName, long Length, Func<Stream> OpenReadStream);

public sealed record AnalysisProviderDto(
    string Id,
    string Name,
    bool Available,
    bool External,
    string? Model,
    string Description);

public sealed record CreateResumeAnalysisRequest(
    Guid ResumeId,
    Guid JobOfferId,
    string Provider,
    bool ConsentToExternalProcessing);

public sealed record ResumeAnalysisSummaryDto(
    Guid Id,
    Guid ResumeId,
    string ResumeFileName,
    Guid? JobOfferId,
    string JobTitle,
    string CompanyName,
    string Provider,
    string Model,
    int MatchScore,
    DateTimeOffset CreatedAt);

public sealed record ResumeAnalysisDto(ResumeAnalysisSummaryDto Summary, string PromptVersion, ResumeAnalysisResult Result);

public sealed class ResumeAnalysisQuery
{
    public Guid? ResumeId { get; init; }
    public Guid? JobOfferId { get; init; }
}

public sealed class CreateResumeAnalysisRequestValidator : AbstractValidator<CreateResumeAnalysisRequest>
{
    public CreateResumeAnalysisRequestValidator()
    {
        RuleFor(r => r.ResumeId).NotEmpty();
        RuleFor(r => r.JobOfferId).NotEmpty();
        RuleFor(r => r.Provider).NotEmpty().MaximumLength(50);
    }
}
