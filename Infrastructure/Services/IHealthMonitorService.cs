namespace Infrastructure.Services;

using Infrastructure.Models;

public interface IHealthMonitorService
{
    HealthSnapshot? LastSnapshot { get; }
}
