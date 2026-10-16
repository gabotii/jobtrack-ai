using JobTrack.Api.Web;
using JobTrack.Application.Resumes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobTrack.Api.Controllers;

/// <summary>CV-vs-offer analyses. The score is indicative and is not a probability of being hired.</summary>
[ApiController]
[Route("api/v1/resume-analyses")]
[Produces("application/json")]
public sealed class ResumeAnalysesController(ResumeAnalysesService analyses) : ControllerBase
{
    /// <summary>Available analyzers. The AI provider is reported as unavailable when no API key is configured.</summary>
    [HttpGet("providers")]
    public IReadOnlyList<AnalysisProviderDto> Providers() => analyses.GetProviders();

    [HttpGet]
    public Task<IReadOnlyList<ResumeAnalysisSummaryDto>> List([FromQuery] ResumeAnalysisQuery query, CancellationToken ct) =>
        analyses.ListAsync(query, ct);

    [HttpGet("{id:guid}")]
    public Task<ResumeAnalysisDto> Get(Guid id, CancellationToken ct) => analyses.GetAsync(id, ct);

    /// <summary>
    /// Runs an analysis. External providers require <c>consentToExternalProcessing: true</c>; failures of the provider
    /// return 429/502/503/504 with a stable <c>code</c>.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting(RateLimiting.Analyses)]
    [ProducesResponseType<ResumeAnalysisDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ResumeAnalysisDto>> Create(CreateResumeAnalysisRequest request, CancellationToken ct)
    {
        var created = await analyses.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Summary.Id }, created);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await analyses.DeleteAsync(id, ct);
        return NoContent();
    }
}
