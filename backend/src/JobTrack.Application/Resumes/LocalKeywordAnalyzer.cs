using System.Text.RegularExpressions;
using JobTrack.Domain.Entities;

namespace JobTrack.Application.Resumes;

/// <summary>
/// Offline analyzer: no data leaves the server. It checks whether each skill listed in the offer appears in the CV
/// text (with a few common aliases) and weighs required skills twice as much as preferred ones. It is deliberately
/// literal: it cannot judge equivalent experience described with other words, which the result states explicitly.
/// </summary>
public sealed partial class LocalKeywordAnalyzer : IResumeAnalysisService
{
    public const string Id = "local";
    public const string Version = "local-keywords-v1";
    private const int RequiredWeight = 2;
    private const int PreferredWeight = 1;
    private const int MaxDerivedKeywords = 15;
    private const int MaxRecommendations = 6;

    private static readonly Dictionary<string, string[]> Aliases = new(StringComparer.Ordinal)
    {
        ["c#"] = ["csharp", "c sharp"],
        [".net"] = ["dotnet", "net core", "asp.net", "asp.net core"],
        ["javascript"] = ["js", "ecmascript"],
        ["typescript"] = ["ts"],
        ["postgresql"] = ["postgres"],
        ["kubernetes"] = ["k8s"],
        ["react"] = ["react.js", "reactjs"],
        ["node.js"] = ["node", "nodejs"],
        ["sql server"] = ["mssql", "t-sql"],
        ["ingles"] = ["english"],
        ["machine learning"] = ["ml", "aprendizaje automatico"],
        ["ci/cd"] = ["integracion continua", "continuous integration", "github actions", "azure devops", "gitlab ci"],
    };

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "para", "como", "with", "that", "this", "from", "your", "will", "have", "years", "year", "experience",
        "experiencia", "trabajo", "equipo", "empresa", "buscamos", "persona", "perfil", "puesto", "requisitos",
        "valorable", "ofrecemos", "sobre", "entre", "desde", "hasta", "nuestro", "nuestra", "nuestros", "nuestras",
        "tambien", "ademas", "todos", "todas", "cada", "donde", "cuando", "team", "work", "working", "role",
        "skills", "knowledge", "conocimientos", "conocimiento", "anos", "minimo", "least", "strong", "good", "able",
        "debe", "deben", "tener", "contar", "parte", "forma", "manera", "mediante", "dentro", "otros", "otras",
        "would", "should", "about", "their", "they", "them", "what", "more", "than", "into", "over", "plus",
        "remoto", "hibrido", "presencial", "salario", "contrato", "jornada", "completa", "beneficios",
    };

    public string ProviderId => Id;
    public string DisplayName => "Análisis básico por palabras clave (local)";
    public bool IsExternal => false;
    public bool IsAvailable => true;
    public string? ModelName => Version;

    public Task<ResumeAnalysisOutcome> AnalyzeAsync(ResumeAnalysisInput input, CancellationToken ct)
    {
        var resume = TextSearch.Normalize(input.ResumeText);
        var resumeSegments = Segments(input.ResumeText);
        var offerSegments = Segments(input.JobDescription);
        var offerText = (input.JobDescription ?? string.Empty).ToLowerInvariant();

        var terms = input.RequiredSkills.Select(s => (Skill: s, Required: true))
            .Concat(input.PreferredSkills.Select(s => (Skill: s, Required: false)))
            .DistinctBy(t => TextSearch.Normalize(t.Skill))
            .ToList();
        var derived = terms.Count == 0;
        if (derived)
        {
            // The offer lists no skills: fall back to its most frequent meaningful words.
            terms = TopKeywords(offerText, MaxDerivedKeywords).Select(k => (Skill: k, Required: true)).ToList();
        }

        var matched = new List<SkillMatch>();
        var missing = new List<MissingSkill>();
        var totalWeight = 0;
        var matchedWeight = 0;
        var requiredMatched = 0;

        foreach (var (skill, required) in terms)
        {
            var weight = required ? RequiredWeight : PreferredWeight;
            totalWeight += weight;
            var offerEvidence = FindEvidence(offerSegments, skill, out _);
            var resumeEvidence = FindEvidence(resumeSegments, skill, out var found);
            if (found)
            {
                matchedWeight += weight;
                requiredMatched += required ? 1 : 0;
                matched.Add(new SkillMatch(skill, resumeEvidence, offerEvidence));
            }
            else
            {
                missing.Add(new MissingSkill(skill, required, offerEvidence));
            }
        }

        var score = totalWeight == 0 ? 0 : (int)Math.Round(100.0 * matchedWeight / totalWeight);
        var requiredTotal = terms.Count(t => t.Required);
        var missingRequired = missing.Where(m => m.Required).Select(m => m.Skill).ToList();

        var experience = resumeSegments
            .Where(segment => segment.Length >= 15 &&
                              matched.Any(m => TextSearch.FindTerm(TextSearch.Normalize(segment), m.Skill) >= 0))
            .Take(5)
            .Select(segment => new ExperienceEvidence("Fragmento del CV que menciona habilidades de la oferta.", segment))
            .ToList();

        var strengths = new List<string>();
        if (matched.Count > 0)
        {
            strengths.Add($"Tu CV menciona {matched.Count} de las {terms.Count} habilidades buscadas: " +
                          string.Join(", ", matched.Take(8).Select(m => m.Skill)) + ".");
        }

        if (!derived && requiredTotal > 0 && requiredMatched == requiredTotal)
        {
            strengths.Add("Aparecen todas las habilidades marcadas como requeridas.");
        }

        var improvements = missingRequired
            .Take(MaxRecommendations)
            .Select(skill => $"No se encontró «{skill}» en el texto del CV.")
            .ToList();

        var recommendations = missingRequired
            .Take(MaxRecommendations)
            .Select(skill => $"Si tienes experiencia real con «{skill}», menciónala de forma explícita, con un proyecto o " +
                             "logro concreto. Si no la tienes, no la añadas: valora si puedes compensarla con otra experiencia.")
            .ToList();
        if (matched.Count > 0)
        {
            recommendations.Add("Usa los mismos términos que la oferta para las habilidades que ya tienes (los filtros " +
                                "automáticos suelen buscar coincidencias literales).");
        }

        var summary = derived
            ? "La oferta no tiene habilidades registradas, así que se compararon sus palabras más frecuentes con tu CV. " +
              "Añade habilidades a la oferta para un resultado más útil."
            : $"Coincidencia orientativa del {score} % según las habilidades de la oferta que aparecen en tu CV.";
        summary += " Es un análisis por palabras clave: no interpreta sinónimos ni experiencia descrita con otras " +
                   "palabras, y no predice tus probabilidades de contratación.";

        var missingKeywords = derived
            ? []
            : TopKeywords(offerText, 30).Where(k => TextSearch.FindTerm(resume, k) < 0).Take(10).ToList();

        var result = new ResumeAnalysisResult(
            score,
            summary,
            matched,
            missing,
            experience,
            missingKeywords,
            strengths,
            improvements,
            recommendations);

        return Task.FromResult(new ResumeAnalysisOutcome(result, Version, Version));
    }

    /// <summary>Returns the first original-text segment (line, sentence or bullet) mentioning the skill or an alias.</summary>
    private static string? FindEvidence(IReadOnlyList<string> segments, string skill, out bool found)
    {
        string[] candidates = [skill, .. Aliases.GetValueOrDefault(TextSearch.Normalize(skill)) ?? []];
        foreach (var segment in segments)
        {
            var normalized = TextSearch.Normalize(segment);
            if (candidates.Any(term => TextSearch.FindTerm(normalized, term) >= 0))
            {
                found = true;
                return segment;
            }
        }

        found = false;
        return null;
    }

    /// <summary>Splits text into short literal segments usable as quotes; long ones are cut on word boundaries.</summary>
    private static List<string> Segments(string? text)
    {
        var segments = new List<string>();
        foreach (var piece in SegmentSeparators().Split(text ?? string.Empty))
        {
            var remaining = piece.Trim();
            while (remaining.Length > 280)
            {
                var cut = remaining.LastIndexOf(' ', 250);
                cut = cut <= 0 ? 250 : cut;
                segments.Add(remaining[..cut].Trim());
                remaining = remaining[cut..].Trim();
            }

            if (remaining.Length >= 3)
            {
                segments.Add(remaining);
            }
        }

        return segments;
    }

    private static List<string> TopKeywords(string lowercaseText, int count) =>
        Words().Matches(lowercaseText)
            .Select(m => m.Value.Trim('.', '-'))
            .Where(w => w.Length >= 4 && !StopWords.Contains(TextSearch.Normalize(w)) && !w.All(char.IsDigit))
            .GroupBy(w => w)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Take(count)
            .Select(g => g.Key)
            .ToList();

    [GeneratedRegex(@"[\p{L}\p{N}][\p{L}\p{N}+#.\-]*")]
    private static partial Regex Words();

    [GeneratedRegex(@"\r?\n|(?<=[.;!?])\s+|[•·▪●]")]
    private static partial Regex SegmentSeparators();
}
