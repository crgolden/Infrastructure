namespace Infrastructure.Controllers;

using Infrastructure.Models;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public sealed class StatusController : ControllerBase
{
    private readonly IHealthMonitorService _healthMonitorService;

    public StatusController(IHealthMonitorService healthMonitorService)
    {
        _healthMonitorService = healthMonitorService;
    }

    [HttpGet]
    public ActionResult<HealthSnapshot> Get()
    {
        var snapshot = _healthMonitorService.LastSnapshot;
        return snapshot is null ? StatusCode(StatusCodes.Status503ServiceUnavailable) : Ok(snapshot);
    }
}
