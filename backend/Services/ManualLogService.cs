using backend.Models;
using Microsoft.Data.SqlClient;

namespace backend.Services;

public class ManualLogService
{
    private readonly string _connectionString;

    public ManualLogService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    public async Task LogAsync(string tagName, string value)
    {
        const string sql = @"
INSERT INTO dbo.Manual (TAG_NAME, [VALUE], DATE_TIME)
VALUES (@TagName, @Value, GETDATE());";

        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(sql, connection);

        command.Parameters.AddWithValue("@TagName", tagName ?? "");
        command.Parameters.AddWithValue("@Value", value ?? "");

        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();
    }

    public async Task LogManyAsync(List<ManualTagItem> items)
    {
        if (items == null || items.Count == 0)
            return;

        const string sql = @"
INSERT INTO dbo.Manual (TAG_NAME, [VALUE], DATE_TIME)
VALUES (@TagName, @Value, GETDATE());";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        foreach (var item in items)
        {
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@TagName", item.TagName ?? "");
            command.Parameters.AddWithValue("@Value", item.Value ?? "");
            await command.ExecuteNonQueryAsync();
        }
    }

    public async Task LogLiveSnapshotAsync(List<TagData> items)
    {
        if (items == null || items.Count == 0)
            return;

        const string sql = @"
INSERT INTO dbo.Manual (TAG_NAME, [VALUE], DATE_TIME)
VALUES (@TagName, @Value, GETDATE());";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        foreach (var item in items)
        {
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@TagName", item.TagName ?? "");
            command.Parameters.AddWithValue("@Value", item.Value ?? "");
            await command.ExecuteNonQueryAsync();
        }
    }
}