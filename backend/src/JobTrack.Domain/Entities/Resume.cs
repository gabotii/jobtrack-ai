using JobTrack.Domain.Common;

namespace JobTrack.Domain.Entities;

/// <summary>
/// An uploaded CV. The binary file lives in private storage under <see cref="StorageKey"/>; the extracted text is kept
/// so analyses do not need to re-read the file.
/// </summary>
public sealed class Resume : OwnedEntity
{
    public const int MaxFileNameLength = 255;

    private Resume()
    {
    }

    public Resume(
        Guid userId,
        string fileName,
        string contentType,
        long sizeBytes,
        string storageKey,
        string sha256,
        string extractedText)
    {
        UserId = userId;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        StorageKey = storageKey;
        Sha256 = sha256;
        ExtractedText = extractedText;
    }

    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string Sha256 { get; private set; } = string.Empty;
    public string ExtractedText { get; private set; } = string.Empty;

    public List<ResumeAnalysis> Analyses { get; private set; } = [];
}
