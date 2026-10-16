using JobTrack.Domain.Entities;

namespace JobTrack.Application.Resumes;

/// <summary>
/// Validates and cleans any analyzer output before it is stored:
/// <list type="bullet">
/// <item>scores are clamped to 0–100, strings trimmed and length-limited, lists capped and de-duplicated;</item>
/// <item>evidence quotes that do not literally appear in the CV (or the offer) are dropped, so the stored result can
/// never cite text the candidate did not write;</item>
/// <item>"matched" skills are kept only if the CV actually supports them (verified quote or the term itself).</item>
/// </list>
/// </summary>
public static class ResumeAnalysisResultSanitizer
{
    public const int MaxSkills = 50;
    public const int MaxListItems = 10;
    public const int MaxSkillLength = 80;
    public const int MaxEvidenceLength = 400;
    public const int MaxItemLength = 600;
    public const int MaxSummaryLength = 1_500;

    public static ResumeAnalysisResult Sanitize(ResumeAnalysisResult raw, ResumeAnalysisInput input)
    {
        var resume = TextSearch.Normalize(input.ResumeText);
        var offer = TextSearch.Normalize(string.Join(
            "\n",
            [input.JobTitle, input.JobDescription ?? string.Empty, .. input.RequiredSkills, .. input.PreferredSkills]));

        var matched = (raw.MatchedSkills ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s?.Skill))
            .Select(s => new SkillMatch(
                Clip(s.Skill, MaxSkillLength)!,
                Verified(resume, s.ResumeEvidence),
                Verified(offer, s.OfferEvidence)))
            .Where(s => s.ResumeEvidence is not null || TextSearch.FindTerm(resume, s.Skill) >= 0)
            .DistinctBy(s => TextSearch.Normalize(s.Skill))
            .Take(MaxSkills)
            .ToList();

        var matchedNames = matched.Select(s => TextSearch.Normalize(s.Skill)).ToHashSet();
        var missing = (raw.MissingSkills ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s?.Skill))
            .Select(s => new MissingSkill(Clip(s.Skill, MaxSkillLength)!, s.Required, Verified(offer, s.OfferEvidence)))
            .Where(s => !matchedNames.Contains(TextSearch.Normalize(s.Skill)))
            .DistinctBy(s => TextSearch.Normalize(s.Skill))
            .Take(MaxSkills)
            .ToList();

        var experience = (raw.RelevantExperience ?? [])
            .Where(e => !string.IsNullOrWhiteSpace(e?.Description))
            .Select(e => new ExperienceEvidence(Clip(e.Description, MaxItemLength)!, Verified(resume, e.ResumeEvidence)))
            .Take(MaxListItems)
            .ToList();

        return new ResumeAnalysisResult(
            Math.Clamp(raw.MatchScore, 0, 100),
            Clip(raw.Summary, MaxSummaryLength) ?? string.Empty,
            matched,
            missing,
            experience,
            CleanList(raw.MissingKeywords, MaxSkillLength).Where(k => TextSearch.FindTerm(resume, k) < 0).ToList(),
            CleanList(raw.Strengths, MaxItemLength),
            CleanList(raw.ImprovementAreas, MaxItemLength),
            CleanList(raw.Recommendations, MaxItemLength));
    }

    private static string? Verified(string normalizedSource, string? quote)
    {
        var clipped = Clip(quote, MaxEvidenceLength);
        return clipped is not null && TextSearch.ContainsQuote(normalizedSource, clipped) ? clipped : null;
    }

    private static List<string> CleanList(IReadOnlyList<string>? items, int maxLength) =>
        (items ?? [])
            .Select(i => Clip(i, maxLength))
            .Where(i => i is not null)
            .Select(i => i!)
            .DistinctBy(TextSearch.Normalize)
            .Take(MaxListItems)
            .ToList();

    private static string? Clip(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..(maxLength - 1)].TrimEnd() + "…";
    }
}
