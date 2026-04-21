using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpcLabs.EasyOpc.DataAccess;

namespace backend.Services;

public class Page2TriggerMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<Page2TriggerMonitorService> _logger;

    private const string TriggerTagName = "ML_MTB_Andon.ShiftTimeSetting.Test.TRIGGER";
    private bool _previousTriggerState = false;

    public Page2TriggerMonitorService(
        IServiceScopeFactory scopeFactory,
        ILogger<Page2TriggerMonitorService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(2000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var opcRuntimeService = scope.ServiceProvider.GetRequiredService<OpcRuntimeService>();
                var triggerLoggingService = scope.ServiceProvider.GetRequiredService<Page2TriggerLoggingService>();

                using var client = new EasyDAClient();
                var vtq = client.ReadItem(
                    opcRuntimeService.MachineName,
                    opcRuntimeService.ServerName,
                    TriggerTagName
                );

                var currentTriggerState = IsTriggerHigh(vtq.Value);

                if (currentTriggerState && !_previousTriggerState)
                {
                    await triggerLoggingService.LogPage2SnapshotAsync();
                    _logger.LogInformation("Page2 trigger changed FALSE -> TRUE. Logged once.");
                }

                if (!currentTriggerState && _previousTriggerState)
                {
                    _logger.LogInformation("Page2 trigger changed TRUE -> FALSE. No logging.");
                }

                _previousTriggerState = currentTriggerState;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while monitoring Page 2 trigger.");
            }

            await Task.Delay(500, stoppingToken);
        }
    }

    private static bool IsTriggerHigh(object? value)
    {
        if (value == null) return false;
        if (value is bool b) return b;

        var text = Convert.ToString(value)?.Trim();
        return text == "1" ||
               string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(text, "high", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(text, "on", StringComparison.OrdinalIgnoreCase);
    }
}