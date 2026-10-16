using JobTrack.Application.Calendar;
using Microsoft.AspNetCore.Mvc;

namespace JobTrack.Api.Controllers;

/// <summary>Interviews, calls and follow-ups. Date-times are ISO 8601 with an explicit offset and returned in UTC.</summary>
[ApiController]
[Route("api/v1/calendar/events")]
[Produces("application/json")]
public sealed class CalendarEventsController(CalendarEventService events) : ControllerBase
{
    /// <summary>Events overlapping [from, to). The range may span at most 366 days.</summary>
    [HttpGet]
    public Task<IReadOnlyList<CalendarEventDto>> List([FromQuery] CalendarEventQuery query, CancellationToken ct) =>
        events.ListAsync(query, ct);

    /// <summary>Scheduled events that have not ended yet, soonest first.</summary>
    [HttpGet("upcoming")]
    public Task<IReadOnlyList<CalendarEventDto>> Upcoming(
        [FromQuery] int limit = 5,
        [FromQuery] Guid? applicationId = null,
        CancellationToken ct = default) =>
        events.ListUpcomingAsync(limit, applicationId, ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<CalendarEventDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<CalendarEventDto> Get(Guid id, CancellationToken ct) => events.GetAsync(id, ct);

    [HttpPost]
    [ProducesResponseType<CalendarEventDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CalendarEventDto>> Create(SaveCalendarEventRequest request, CancellationToken ct)
    {
        var created = await events.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public Task<CalendarEventDto> Update(Guid id, SaveCalendarEventRequest request, CancellationToken ct) =>
        events.UpdateAsync(id, request, ct);

    /// <summary>Marks the event as scheduled, completed or cancelled.</summary>
    [HttpPut("{id:guid}/status")]
    public Task<CalendarEventDto> ChangeStatus(Guid id, ChangeCalendarEventStatusRequest request, CancellationToken ct) =>
        events.ChangeStatusAsync(id, request, ct);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await events.DeleteAsync(id, ct);
        return NoContent();
    }
}
