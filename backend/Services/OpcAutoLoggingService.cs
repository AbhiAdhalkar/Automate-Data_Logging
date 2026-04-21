namespace backend.Services;

public class OpcAutoLoggingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OpcAutoLoggingService> _logger;

    public OpcAutoLoggingService(
        IServiceScopeFactory scopeFactory,
        ILogger<OpcAutoLoggingService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var hasTicked = await timer.WaitForNextTickAsync(stoppingToken);
                if (!hasTicked)
                    break;

                using var scope = _scopeFactory.CreateScope();

                var opcRuntime = scope.ServiceProvider.GetRequiredService<OpcRuntimeService>();
                var manualLogService = scope.ServiceProvider.GetRequiredService<ManualLogService>();

                var liveValues = opcRuntime.ReadLiveValues();

                await manualLogService.LogLiveSnapshotAsync(liveValues);

                _logger.LogInformation(
                    "Auto logged {Count} tags at {Time}",
                    liveValues.Count,
                    DateTime.Now
                );
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Automatic OPC logging failed.");
            }
        }
    }
}