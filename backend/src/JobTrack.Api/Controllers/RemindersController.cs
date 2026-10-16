using JobTrack.Application.Common;
using JobTrack.Application.Reminders;
using Microsoft.AspNetCore.Mvc;

namespace JobTrack.Api.Controllers;

[ApiController]
[Route("api/v1/reminders")]
[Produces("application/json")]
public sealed class RemindersController(ReminderService reminders) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ReminderDto>> List([FromQuery] ReminderQuery query, CancellationToken ct) =>
        reminders.ListAsync(query, ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ReminderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ReminderDto> Get(Guid id, CancellationToken ct) => reminders.GetAsync(id, ct);

    [HttpPost]
    [ProducesResponseType<ReminderDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ReminderDto>> Create(SaveReminderRequest request, CancellationToken ct)
    {
        var created = await reminders.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public Task<ReminderDto> Update(Guid id, SaveReminderRequest request, CancellationToken ct) =>
        reminders.UpdateAsync(id, request, ct);

    [HttpPost("{id:guid}/complete")]
    public Task<ReminderDto> Complete(Guid id, CancellationToken ct) => reminders.CompleteAsync(id, ct);

    [HttpPost("{id:guid}/reopen")]
    public Task<ReminderDto> Reopen(Guid id, CancellationToken ct) => reminders.ReopenAsync(id, ct);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await reminders.DeleteAsync(id, ct);
        return NoContent();
    }
}
