using JobTrack.Application.Common;
using JobTrack.Application.JobOffers;
using Microsoft.AspNetCore.Mvc;

namespace JobTrack.Api.Controllers;

/// <summary>Saved job offers (independent of whether the user has applied).</summary>
[ApiController]
[Route("api/v1/jobs")]
[Produces("application/json")]
public sealed class JobsController(JobOfferService jobs) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<JobOfferListItemDto>> List([FromQuery] JobOfferQuery query, CancellationToken ct) =>
        jobs.ListAsync(query, ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<JobOfferDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<JobOfferDto> Get(Guid id, CancellationToken ct) => jobs.GetAsync(id, ct);

    [HttpPost]
    [ProducesResponseType<JobOfferDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<JobOfferDto>> Create(SaveJobOfferRequest request, CancellationToken ct)
    {
        var offer = await jobs.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = offer.Id }, offer);
    }

    [HttpPut("{id:guid}")]
    public Task<JobOfferDto> Update(Guid id, SaveJobOfferRequest request, CancellationToken ct) =>
        jobs.UpdateAsync(id, request, ct);

    /// <summary>Deletes an offer. Returns 409 while it has an application.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await jobs.DeleteAsync(id, ct);
        return NoContent();
    }
}
