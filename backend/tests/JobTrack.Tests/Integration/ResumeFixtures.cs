using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PageSize = UglyToad.PdfPig.Content.PageSize;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace JobTrack.Tests.Integration;

/// <summary>Builds real PDF and DOCX files in memory so extraction is exercised end to end.</summary>
public static class ResumeFixtures
{
    public static readonly string[] CvLines =
    [
        "Ana Torres - Desarrolladora Full-Stack",
        "Experiencia: 5 anos desarrollando APIs con C# y ASP.NET Core sobre PostgreSQL.",
        "Frontend con React y TypeScript; pruebas automatizadas con xUnit.",
        "Despliegues con Docker en entornos de integracion continua.",
        "Idiomas: espanol nativo, ingles profesional.",
    ];

    public static byte[] Docx(params string[] lines)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new Document(new Body(
                (lines.Length == 0 ? CvLines : lines).Select(line => new Paragraph(new Run(new Text(line))))));
        }

        return stream.ToArray();
    }

    public static byte[] Pdf(params string[] lines)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        var y = 780d;
        foreach (var line in lines.Length == 0 ? CvLines : lines)
        {
            page.AddText(line, 11, new PdfPoint(40, y), font);
            y -= 18;
        }

        return builder.Build();
    }

    public static MultipartFormDataContent Upload(byte[] content, string fileName, string contentType = "application/octet-stream")
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }
}
