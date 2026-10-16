using System.Security.Cryptography;
using System.Text.RegularExpressions;
using JobTrack.Application.Abstractions;
using JobTrack.Application.Common;
using JobTrack.Domain.Common;
using JobTrack.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobTrack.Application.Resumes;

/// <summary>
/// CV upload and management. Files are validated by size, extension and content signature (the client-declared
/// content type is ignored), stored privately under a server-generated key, and their text is extracted once.
/// Deleting a CV deletes the file, the extracted text and every analysis derived from it.
/// Log messages never include file contents or extracted text.
/// </summary>
public sealed partial class ResumeService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IFileStorage storage,
    IResumeTextExtractor extractor,
    ILogger<ResumeService> logger)
{
    public const long MaxFileBytes = 5 * 1024 * 1024;
    public const int MaxResumesPerUser = 20;
    public const int MinTextLength = 100;
    public const int MaxTextLength = 100_000;

    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];

    public async Task<IReadOnlyList<ResumeDto>> ListAsync(CancellationToken ct) =>
        await db.Resumes.AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ResumeDto(r.Id, r.FileName, r.ContentType, r.SizeBytes, r.ExtractedText.Length,
                r.Analyses.Count, r.CreatedAt))
            .ToListAsync(ct);

    public async Task<ResumeDetailDto> GetAsync(Guid id, CancellationToken ct) =>
        await db.Resumes.AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new ResumeDetailDto(
                new ResumeDto(r.Id, r.FileName, r.ContentType, r.SizeBytes, r.ExtractedText.Length, r.Analyses.Count,
                    r.CreatedAt),
                r.ExtractedText))
            .SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException("CV");

    public async Task<ResumeDto> UploadAsync(ResumeUpload upload, CancellationToken ct)
    {
        if (upload.Length <= 0)
        {
            throw new DomainException("resume.empty", "El archivo está vacío.");
        }

        if (upload.Length > MaxFileBytes)
        {
            throw new DomainException("resume.too_large", "El archivo supera el tamaño máximo de 5 MB.");
        }

        var extension = Path.GetExtension(upload.FileName).ToLowerInvariant();
        if (extension is not (".pdf" or ".docx"))
        {
            throw new DomainException("resume.unsupported_type", "Solo se admiten archivos PDF o DOCX.");
        }

        var bytes = await ReadAllAsync(upload, ct);
        var kind = DetectKind(bytes);
        if (kind is null || (kind == ResumeFileKind.Pdf) != (extension == ".pdf"))
        {
            throw new DomainException(
                "resume.invalid_content",
                "El contenido del archivo no corresponde a un PDF o DOCX válido.");
        }

        var userId = currentUser.RequiredUserId;
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (await db.Resumes.AnyAsync(r => r.Sha256 == hash, ct))
        {
            throw new ConflictException("resume.duplicate", "Ya subiste este mismo archivo.");
        }

        if (await db.Resumes.CountAsync(ct) >= MaxResumesPerUser)
        {
            throw new ConflictException(
                "resume.limit_reached",
                $"Puedes guardar hasta {MaxResumesPerUser} CV. Elimina alguno antes de subir otro.");
        }

        string text;
        using (var stream = new MemoryStream(bytes, writable: false))
        {
            text = CleanText(await extractor.ExtractAsync(stream, kind.Value, ct));
        }

        if (text.Length < MinTextLength)
        {
            throw new DomainException(
                "resume.no_text",
                "No se pudo extraer texto del archivo. Si es un PDF escaneado (imagen), exporta el CV como texto o DOCX.");
        }

        if (text.Length > MaxTextLength)
        {
            throw new DomainException("resume.too_much_text", "El documento contiene demasiado texto para ser un CV.");
        }

        var key = $"{userId:N}/{Guid.NewGuid():N}{extension}";
        var contentType = kind == ResumeFileKind.Pdf
            ? "application/pdf"
            : "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        using (var stream = new MemoryStream(bytes, writable: false))
        {
            await storage.SaveAsync(key, stream, ct);
        }

        var resume = new Resume(userId, SafeFileName(upload.FileName, extension), contentType, bytes.Length, key, hash, text);
        db.Resumes.Add(resume);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(key, CancellationToken.None);
            throw;
        }

        logger.LogInformation("Resume {ResumeId} uploaded ({SizeBytes} bytes, {Kind})", resume.Id, bytes.Length, kind);
        return (await GetAsync(resume.Id, ct)).Resume;
    }

    public async Task<(Stream Content, string FileName, string ContentType)> OpenFileAsync(Guid id, CancellationToken ct)
    {
        var resume = await db.Resumes.AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new { r.StorageKey, r.FileName, r.ContentType })
            .SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("CV");

        return (await storage.OpenReadAsync(resume.StorageKey, ct), resume.FileName, resume.ContentType);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var resume = await db.Resumes.SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("CV");

        db.Resumes.Remove(resume);
        await db.SaveChangesAsync(ct);

        try
        {
            await storage.DeleteAsync(resume.StorageKey, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // The database row is already gone; an orphaned file is logged for cleanup rather than failing the request.
            logger.LogError(ex, "Could not delete the stored file of resume {ResumeId}", id);
        }

        logger.LogInformation("Resume {ResumeId} deleted with its analyses", id);
    }

    internal static ResumeFileKind? DetectKind(ReadOnlySpan<byte> bytes) =>
        bytes.StartsWith(PdfSignature) ? ResumeFileKind.Pdf
        : bytes.StartsWith(ZipSignature) ? ResumeFileKind.Docx
        : null;

    internal static string CleanText(string text)
    {
        var unified = text.Replace("\r\n", "\n").Replace('\r', '\n').Replace('\0', ' ');
        var lines = unified.Split('\n').Select(line => SpaceRuns().Replace(line, " ").Trim());
        return BlankLineRuns().Replace(string.Join('\n', lines), "\n\n").Trim();
    }

    private static string SafeFileName(string fileName, string extension)
    {
        var name = Path.GetFileNameWithoutExtension(Path.GetFileName(fileName));
        name = new string(name.Where(c => !char.IsControl(c) && !Path.GetInvalidFileNameChars().Contains(c)).ToArray()).Trim();
        if (name.Length == 0)
        {
            name = "cv";
        }

        var maxLength = Resume.MaxFileNameLength - extension.Length;
        return (name.Length > maxLength ? name[..maxLength] : name) + extension;
    }

    private static async Task<byte[]> ReadAllAsync(ResumeUpload upload, CancellationToken ct)
    {
        await using var source = upload.OpenReadStream();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaxFileBytes)
            {
                throw new DomainException("resume.too_large", "El archivo supera el tamaño máximo de 5 MB.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    [GeneratedRegex(@"[ \t\f\v ]+")]
    private static partial Regex SpaceRuns();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLineRuns();
}
