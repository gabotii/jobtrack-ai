using JobTrack.Domain.Common;

namespace JobTrack.Domain.Entities;

/// <summary>
/// Stored result of comparing a resume with a job offer. Offer title and company are snapshotted so the history stays
/// readable if the offer is later deleted. Deleting the resume deletes its analyses (they quote the CV).
/// </summary>
public sealed class ResumeAnalysis : OwnedEntity
{
    private ResumeAnalysis()
    {
    }

    public ResumeAnalysis(
        Guid userId,
        Guid resumeId,
        Guid? jobOfferId,
        string jobTitleSnapshot,
        string companyNameSnapshot,
        string provider,
        string model,
        string promptVersion,
        ResumeAnalysisResult result)
    {
        UserId = userId;
        ResumeId = resumeId;
        JobOfferId = jobOfferId;
        JobTitleSnapshot = jobTitleSnapshot;
        CompanyNameSnapshot = companyNameSnapshot;
        Provider = provider;
        Model = model;
        PromptVersion = promptVersion;
        MatchScore = result.MatchScore;
        Result = result;
    }

    public Guid ResumeId { get; private set; }
    public Resume? Resume { get; private set; }
    public Guid? JobOfferId { get; private set; }
    public JobOffer? JobOffer { get; private set; }
    public string JobTitleSnapshot { get; private set; } = string.Empty;
    public string CompanyNameSnapshot { get; private set; } = string.Empty;

    /// <summary>Analyzer that produced the result, e.g. "local-keywords" or "anthropic".</summary>
    public string Provider { get; private set; } = string.Empty;

    public string Model { get; private set; } = string.Empty;
    public string PromptVersion { get; private set; } = string.Empty;

    /// <summary>Indicative 0–100 compatibility score. It is not a probability of being hired.</summary>
    public int MatchScore { get; private set; }

    public ResumeAnalysisResult Result { get; private set; } = ResumeAnalysisResult.Empty;
}

/// <summary>Structured analysis output. Evidence fields hold short literal quotes from the CV or the offer.</summary>
public sealed record ResumeAnalysisResult(
    int MatchScore,
    string Summary,
    IReadOnlyList<SkillMatch> MatchedSkills,
    IReadOnlyList<MissingSkill> MissingSkills,
    IReadOnlyList<ExperienceEvidence> RelevantExperience,
    IReadOnlyList<string> MissingKeywords,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> ImprovementAreas,
    IReadOnlyList<string> Recommendations)
{
    public static readonly ResumeAnalysisResult Empty = new(0, string.Empty, [], [], [], [], [], [], []);
}

public sealed record SkillMatch(string Skill, string? ResumeEvidence, string? OfferEvidence);

/// <summary>A skill the offer asks for that was not found in the CV text. Absence in the text does not prove the candidate lacks it.</summary>
public sealed record MissingSkill(string Skill, bool Required, string? OfferEvidence);

public sealed record ExperienceEvidence(string Description, string? ResumeEvidence);
