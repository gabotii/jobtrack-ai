using JobTrack.Application.Applications;
using JobTrack.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace JobTrack.Api.Controllers;

[ApiController]
[Route("api/v1/applications")]
[Produces("application/json")]
public sealed class ApplicationsController(ApplicationService applications) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ApplicationSummaryDto>> List([FromQuery] ApplicationQuery query, CancellationToken ct) =>
        applications.ListAsync(query, ct);

    /// <summary>All cards for the Kanban board (up to 500), most recently moved first.</summary>
    [HttpGet("board")]
    public Task<IReadOnlyList<ApplicationSummaryDto>> Board([FromQuery] ApplicationBoardQuery query, CancellationToken ct) =>
        applications.GetBoardAsync(query, ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ApplicationDetailDto> Get(Guid id, CancellationToken ct) => applications.GetAsync(id, ct);

    /// <summary>Creates the application for a job offer. Returns 409 if the offer already has one.</summary>
    [HttpPost]
    [ProducesResponseType<ApplicationDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApplicationDetailDto>> Create(CreateApplicationRequest request, CancellationToken ct)
    {
        var application = await applications.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = application.Summary.Id }, application);
    }

    [HttpPut("{id:guid}")]
    public Task<ApplicationDetailDto> Update(Guid id, UpdateApplicationRequest request, CancellationToken ct) =>
        applications.UpdateAsync(id, request, ct);

    /// <summary>Moves the application to another status and records it in the history. Invalid transitions return 422.</summary>
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType<ApplicationSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<ApplicationSummaryDto> ChangeStatus(
        Guid id,
        ChangeApplicationStatusRequest request,
        CancellationToken ct) =>
        applications.ChangeStatusAsync(id, request, ct);

    [HttpGet("{id:guid}/history")]
    public Task<IReadOnlyList<ApplicationStatusHistoryDto>> History(Guid id, CancellationToken ct) =>
        applications.GetHistoryAsync(id, ct);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await applications.DeleteAsync(id, ct);
        return NoContent();
    }
}
