namespace Infrastructure.Services;

using Models;

public interface IHealthMonitorService
{
    HealthSnapshot? LastSnapshot { get; }
}
