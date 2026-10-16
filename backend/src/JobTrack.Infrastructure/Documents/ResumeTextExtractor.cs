using System.IO.Packaging;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using JobTrack.Application.Resumes;
using JobTrack.Domain.Common;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;

namespace JobTrack.Infrastructure.Documents;

/// <summary>Plain-text extraction with PdfPig (PDF) and the Open XML SDK (DOCX), with limits against hostile files.</summary>
internal sealed class ResumeTextExtractor : IResumeTextExtractor
{
    public const int MaxPdfPages = 30;

    /// <summary>Caps the decompressed size of any DOCX part (protects against zip bombs).</summary>
    private const long MaxCharactersInPart = 10_000_000;

    public Task<string> ExtractAsync(Stream content, ResumeFileKind kind, CancellationToken ct) =>
        Task.Run(() => kind == ResumeFileKind.Pdf ? ExtractPdf(content, ct) : ExtractDocx(content), ct);

    private static string ExtractPdf(Stream content, CancellationToken ct)
    {
        try
        {
            using var document = PdfDocument.Open(content);
            if (document.NumberOfPages > MaxPdfPages)
            {
                throw new DomainException("resume.too_many_pages", $"El PDF tiene más de {MaxPdfPages} páginas.");
            }

            var text = new StringBuilder();
            foreach (var page in document.GetPages())
            {
                ct.ThrowIfCancellationRequested();
                text.AppendLine(ContentOrderTextExtractor.GetText(page));
            }

            return text.ToString();
        }
        catch (PdfDocumentEncryptedException)
        {
            throw new DomainException("resume.encrypted", "El PDF está protegido con contraseña. Súbelo sin protección.");
        }
        catch (Exception ex) when (ex is PdfDocumentFormatException or InvalidOperationException or ArgumentException
                                       or IndexOutOfRangeException)
        {
            throw new DomainException("resume.invalid_content", "No se pudo leer el PDF. Comprueba que no esté dañado.");
        }
    }

    private static string ExtractDocx(Stream content)
    {
        try
        {
            using var document = WordprocessingDocument.Open(content, false, new OpenSettings
            {
                MaxCharactersInPart = MaxCharactersInPart,
            });
            var body = document.MainDocumentPart?.Document?.Body
                ?? throw new DomainException("resume.invalid_content", "El archivo no es un documento de Word válido.");

            var text = new StringBuilder();
            foreach (var paragraph in body.Descendants<Paragraph>())
            {
                text.AppendLine(paragraph.InnerText);
            }

            return text.ToString();
        }
        catch (Exception ex) when (ex is OpenXmlPackageException or InvalidDataException or FileFormatException
                                       or InvalidOperationException)
        {
            throw new DomainException("resume.invalid_content", "No se pudo leer el DOCX. Comprueba que no esté dañado.");
        }
    }
}
