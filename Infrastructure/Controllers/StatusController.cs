namespace Infrastructure.Controllers;

using Microsoft.AspNetCore.Mvc;
using Models;
using Services;

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
        return snapshot is null ? StatusCode(503) : Ok(snapshot);
    }
}
