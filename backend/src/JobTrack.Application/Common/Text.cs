namespace JobTrack.Application.Common;

internal static class Text
{
    /// <summary>Trims the value and turns blank strings into null so optional fields are stored consistently.</summary>
    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Trims, drops blanks and removes case-insensitive duplicates while keeping the first spelling.</summary>
    public static List<string> CleanList(IEnumerable<string>? values) =>
        (values ?? [])
            .Select(v => v?.Trim())
            .Where(v => !string.IsNullOrEmpty(v))
            .Select(v => v!)
            .DistinctBy(v => v.ToUpperInvariant())
            .ToList();
}
