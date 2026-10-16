using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace JobTrack.Application.Resumes;

/// <summary>Accent-, case- and whitespace-insensitive text matching used to verify evidence and find skills.</summary>
public static partial class TextSearch
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return Whitespace().Replace(builder.ToString(), " ").Trim();
    }

    /// <summary>True when <paramref name="quote"/> literally appears in <paramref name="normalizedSource"/>.</summary>
    public static bool ContainsQuote(string normalizedSource, string? quote)
    {
        var normalizedQuote = Normalize(quote).Trim('.', '…', '"', '\'', ' ');
        return normalizedQuote.Length >= 2 && normalizedSource.Contains(normalizedQuote, StringComparison.Ordinal);
    }

    /// <summary>
    /// Finds a term as a whole token: "java" must not match "javascript", but "c#" and ".net" (with symbols) work.
    /// Returns the index in <paramref name="normalizedSource"/> or -1.
    /// </summary>
    public static int FindTerm(string normalizedSource, string term)
    {
        var normalizedTerm = Normalize(term);
        if (normalizedTerm.Length == 0)
        {
            return -1;
        }

        var start = 0;
        while (start < normalizedSource.Length)
        {
            var index = normalizedSource.IndexOf(normalizedTerm, start, StringComparison.Ordinal);
            if (index < 0)
            {
                return -1;
            }

            var end = index + normalizedTerm.Length;
            var boundaryBefore = index == 0 || !char.IsLetterOrDigit(normalizedSource[index - 1]);
            var boundaryAfter = end >= normalizedSource.Length || !IsWordContinuation(normalizedSource, end);
            if (boundaryBefore && boundaryAfter)
            {
                return index;
            }

            start = index + 1;
        }

        return -1;
    }

    private static bool IsWordContinuation(string text, int position)
    {
        var c = text[position];
        // "react." or "c#," end the term; "reactjs" or "javascript" continue it.
        return char.IsLetterOrDigit(c) || (c is '+' or '#' && position + 1 < text.Length && char.IsLetterOrDigit(text[position + 1]));
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
