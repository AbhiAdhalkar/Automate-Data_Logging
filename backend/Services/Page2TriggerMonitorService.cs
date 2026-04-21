namespace backend.Services;

public class Page2TriggerMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<Page2TriggerMonitorService> _logger;

    public Page2TriggerMonitorService(
        IServiceScopeFactory scopeFactory,
        ILogger<Page2TriggerMonitorService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var hasTicked = await timer.WaitForNextTickAsync(stoppingToken);
                if (!hasTicked)
                    break;

                using var scope = _scopeFactory.CreateScope();
                var triggerService = scope.ServiceProvider.GetRequiredService<Page2TriggerLoggingService>();

                await triggerService.LogPage2SnapshotIfTriggeredAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Page2 trigger monitoring failed.");
            }
        }
    }
}