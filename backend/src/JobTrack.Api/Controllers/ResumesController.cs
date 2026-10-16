using JobTrack.Api.Web;
using JobTrack.Application.Resumes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobTrack.Api.Controllers;

/// <summary>Uploaded CVs (PDF or DOCX, up to 5 MB). Files are private to their owner.</summary>
[ApiController]
[Route("api/v1/resumes")]
[Produces("application/json")]
public sealed class ResumesController(ResumeService resumes) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ResumeDto>> List(CancellationToken ct) => resumes.ListAsync(ct);

    /// <summary>Metadata plus the extracted text, so the user can check what will be analysed.</summary>
    [HttpGet("{id:guid}")]
    public Task<ResumeDetailDto> Get(Guid id, CancellationToken ct) => resumes.GetAsync(id, ct);

    /// <summary>Uploads a CV as multipart/form-data with a single "file" field.</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting(RateLimiting.Uploads)]
    // Slightly above the 5 MB file limit to leave room for the multipart envelope; the service enforces 5 MB exactly.
    [RequestSizeLimit(ResumeService.MaxFileBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = ResumeService.MaxFileBytes + 64 * 1024)]
    [ProducesResponseType<ResumeDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ResumeDto>> Upload(IFormFile file, CancellationToken ct)
    {
        var created = await resumes.UploadAsync(new ResumeUpload(file.FileName, file.Length, file.OpenReadStream), ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Downloads the original file (always as an attachment).</summary>
    [HttpGet("{id:guid}/file")]
    [Produces("application/pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var (content, fileName, contentType) = await resumes.OpenFileAsync(id, ct);
        return File(content, contentType, fileName);
    }

    /// <summary>Deletes the file, its extracted text and all analyses based on it.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await resumes.DeleteAsync(id, ct);
        return NoContent();
    }
}
