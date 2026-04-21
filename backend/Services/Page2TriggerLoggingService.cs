using Microsoft.Data.SqlClient;
using OpcLabs.EasyOpc.DataAccess;

namespace backend.Services;

public class Page2TriggerLoggingService
{
    private readonly string _connectionString;
    private readonly OpcRuntimeService _opcRuntime;
    private readonly ILogger<Page2TriggerLoggingService> _logger;

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

    public async Task LogPage2SnapshotAsync()
    {
        try
        {
            using var client = new EasyDAClient();

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

                    var value = Convert.ToString(vtq.Value) ?? "";

                    await using var command = new SqlCommand(sql, connection);
                    command.Parameters.AddWithValue("@TagName", tagName);
                    command.Parameters.AddWithValue("@Value", value);
                    await command.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to log Page2 tag {TagName}", tagName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Page2 trigger logging failed.");
        }
    }
}