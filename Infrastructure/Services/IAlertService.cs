namespace Infrastructure.Services;

using Models;

public interface IAlertService
{
    Task SendAlertAsync(ServiceHealthResult result, CancellationToken cancellationToken = default);

    Task SendRecoveryAsync(ServiceHealthResult result, CancellationToken cancellationToken = default);
}
