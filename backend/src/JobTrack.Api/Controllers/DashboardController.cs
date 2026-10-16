using JobTrack.Application.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace JobTrack.Api.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Produces("application/json")]
public sealed class DashboardController(DashboardService dashboard) : ControllerBase
{
    /// <summary>Metrics, charts and upcoming items for the current user, computed in their time zone.</summary>
    [HttpGet]
    public Task<DashboardDto> Get(CancellationToken ct) => dashboard.GetAsync(ct);
}
