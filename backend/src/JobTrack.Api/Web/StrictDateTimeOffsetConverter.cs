using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace JobTrack.Api.Web;

/// <summary>
/// Accepts only ISO 8601 instants with an explicit offset ("…Z" or "…+02:00"). Without this, a value such as
/// "2026-03-10T10:00:00" would silently be interpreted in the server's local zone and shift interviews.
/// Values are always written in UTC.
/// </summary>
internal sealed partial class StrictDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (value is null || !OffsetSuffix().IsMatch(value) ||
            !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
        {
            throw new JsonException("Expected an ISO 8601 date-time with an explicit offset, e.g. 2026-03-10T09:00:00Z.");
        }

        return result.ToUniversalTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));

    [GeneratedRegex(@"(Z|[+-]\d{2}:\d{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex OffsetSuffix();
}
