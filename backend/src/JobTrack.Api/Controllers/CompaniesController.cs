using JobTrack.Application.Common;
using JobTrack.Application.Companies;
using Microsoft.AspNetCore.Mvc;

namespace JobTrack.Api.Controllers;

[ApiController]
[Route("api/v1/companies")]
[Produces("application/json")]
public sealed class CompaniesController(CompanyService companies) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<CompanyDto>> List([FromQuery] CompanyQuery query, CancellationToken ct) =>
        companies.ListAsync(query, ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<CompanyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<CompanyDto> Get(Guid id, CancellationToken ct) => companies.GetAsync(id, ct);

    [HttpPost]
    [ProducesResponseType<CompanyDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompanyDto>> Create(SaveCompanyRequest request, CancellationToken ct)
    {
        var company = await companies.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = company.Id }, company);
    }

    [HttpPut("{id:guid}")]
    public Task<CompanyDto> Update(Guid id, SaveCompanyRequest request, CancellationToken ct) =>
        companies.UpdateAsync(id, request, ct);

    /// <summary>Deletes a company. Returns 409 while it still has job offers.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await companies.DeleteAsync(id, ct);
        return NoContent();
    }
}
