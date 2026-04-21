using Microsoft.Data.SqlClient;
using OpcLabs.EasyOpc.DataAccess;

namespace backend.Services;

public class Page2TriggerLoggingService
{
    private readonly string _connectionString;
    private readonly OpcRuntimeService _opcRuntime;
    private readonly ILogger<Page2TriggerLoggingService> _logger;

    private const string TriggerTag = "ML_MTB_Andon.ShiftTimeSetting.Test.TRIGGER";

    private static readonly string[] EventTags =
    {
        "ML_MTB_Andon.ShiftTimeSetting.Test.EVENT_1",
        "ML_MTB_Andon.ShiftTimeSetting.Test.EVENT_2",
        "ML_MTB_Andon.ShiftTimeSetting.Test.EVENT_3",
        "ML_MTB_Andon.ShiftTimeSetting.Test.EVENT_4",
        "ML_MTB_Andon.ShiftTimeSetting.Test.EVENT_5"
    };

    public Page2TriggerLoggingService(
        IConfiguration configuration,
        OpcRuntimeService opcRuntime,
        ILogger<Page2TriggerLoggingService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        _opcRuntime = opcRuntime;
        _logger = logger;
    }

    public async Task LogPage2SnapshotIfTriggeredAsync()
    {
        try
        {
            using var client = new EasyDAClient();

            var triggerVtq = client.ReadItem(
                _opcRuntime.MachineName,
                _opcRuntime.ServerName,
                TriggerTag
            );

            var triggerRaw = triggerVtq.Value;
            var isTriggered = IsTriggerOn(triggerRaw);

            if (!isTriggered)
                return;

            await LogEventTagsAsync(client);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Page2 trigger logging failed.");
        }
    }

    private async Task LogEventTagsAsync(EasyDAClient client)
    {
        const string sql = @"
INSERT INTO dbo.Automatic (TAG_NAME, [VALUE], DATE_TIME)
VALUES (@TagName, @Value, GETDATE());";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        foreach (var tagName in EventTags)
        {
            try
            {
                var vtq = client.ReadItem(
                    _opcRuntime.MachineName,
                    _opcRuntime.ServerName,
                    tagName
                );

                var intValue = ConvertToInt(vtq.Value);

                await using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@TagName", tagName);
                command.Parameters.AddWithValue("@Value", intValue);
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to log automatic tag {TagName}", tagName);
            }
        }
    }

    private static bool IsTriggerOn(object? value)
    {
        if (value == null) return false;

        if (value is bool b) return b;

        var text = Convert.ToString(value)?.Trim();

        if (string.Equals(text, "1", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(text, "on", StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }

    private static int ConvertToInt(object? value)
    {
        if (value == null) return 0;

        if (value is int i) return i;
        if (value is short s) return s;
        if (value is byte b) return b;
        if (value is bool flag) return flag ? 1 : 0;

        var text = Convert.ToString(value)?.Trim();

        if (int.TryParse(text, out var parsed))
            return parsed;

        if (bool.TryParse(text, out var boolParsed))
            return boolParsed ? 1 : 0;

        return 0;
    }
}