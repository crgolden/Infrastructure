namespace Infrastructure.Controllers;

using Infrastructure.Models;
using Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public sealed class StatusController(IHealthMonitorService healthMonitorService) : ControllerBase
{
    [HttpGet]
    public ActionResult<HealthSnapshot> Get()
    {
        var snapshot = healthMonitorService.LastSnapshot;
        return snapshot is null ? StatusCode(503) : Ok(snapshot);
    }
}
