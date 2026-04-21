using backend.Models;
using backend.Services;
using OpcLabs.EasyOpc.DataAccess;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<OpcStore>();
builder.Services.AddSingleton<OpcRuntimeService>();
builder.Services.AddScoped<ManualLogService>();
builder.Services.AddHostedService<OpcAutoLoggingService>();
builder.Services.AddScoped<Page2TriggerLoggingService>();
builder.Services.AddHostedService<Page2TriggerMonitorService>();

var page2Tags = new[]
{
    "ML_MTB_Andon.ShiftTimeSetting.Test.TRIGGER",
    "ML_MTB_Andon.ShiftTimeSetting.Test.EVENT_1",
    "ML_MTB_Andon.ShiftTimeSetting.Test.EVENT_2",
    "ML_MTB_Andon.ShiftTimeSetting.Test.EVENT_3",
    "ML_MTB_Andon.ShiftTimeSetting.Test.EVENT_4",
    "ML_MTB_Andon.ShiftTimeSetting.Test.EVENT_5"
};

var app = builder.Build();

app.UseCors("ReactPolicy");

var opcRuntime = app.Services.GetRequiredService<OpcRuntimeService>();

app.MapGet("/api/tags", (OpcStore store) =>
{
    return Results.Ok(store.GetAll());
});

app.MapGet("/api/tags/{tagName}", (string tagName, OpcStore store) =>
{
    var tag = store.Get(tagName);
    return tag is null ? Results.NotFound() : Results.Ok(tag);
});

app.MapPost("/api/tags/write", (ToggleTagRequest request, OpcRuntimeService opc) =>
{
    if (string.IsNullOrWhiteSpace(request.TagName) || string.IsNullOrWhiteSpace(request.Value))
    {
        return Results.BadRequest(new
        {
            success = false,
            error = "TagName and Value are required."
        });
    }

    try
    {
        var valueToWrite = request.Value;

        var attempts = new List<(string TypeName, object Value)>
        {
            ("String", valueToWrite),
            ("Int32", int.TryParse(valueToWrite, out int i32) ? i32 : 0),
            ("Int16", short.TryParse(valueToWrite, out short s16) ? s16 : (short)0),
            ("Byte", byte.TryParse(valueToWrite, out byte b) ? b : (byte)0),
            ("Boolean", bool.TryParse(valueToWrite, out bool bval) ? bval : !string.IsNullOrEmpty(valueToWrite)),
            ("Double", double.TryParse(valueToWrite, out double dbl) ? dbl : 0.0)
        };

        var errors = new List<object>();

        foreach (var attempt in attempts)
        {
            try
            {
                using var writeClient = new EasyDAClient();

                writeClient.WriteItemValue(
                    opc.MachineName,
                    opc.ServerName,
                    request.TagName,
                    attempt.Value
                );

                return Results.Ok(new
                {
                    success = true,
                    tagName = request.TagName,
                    value = valueToWrite,
                    writtenAs = attempt.TypeName
                });
            }
            catch (Exception ex)
            {
                errors.Add(new { typeTried = attempt.TypeName, error = ex.Message });
            }
        }

        return Results.BadRequest(new
        {
            success = false,
            tagName = request.TagName,
            value = valueToWrite,
            error = "All write attempts failed.",
            details = errors
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new
        {
            success = false,
            error = ex.Message
        });
    }
});

app.MapPost("/api/tags/manual-save", async (List<ManualTagItem> items, ManualLogService logService) =>
{
    try
    {
        if (items == null || items.Count == 0)
        {
            return Results.BadRequest(new
            {
                success = false,
                error = "No tag values received."
            });
        }

        await logService.LogManyAsync(items);

        return Results.Ok(new
        {
            success = true,
            count = items.Count,
            message = "Saved successfully"
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Manual save failed",
            detail: ex.Message,
            statusCode: 500
        );
    }
});

app.MapGet("/api/tags/live", (OpcRuntimeService opc) =>
{
    try
    {
        var result = opc.ReadLiveValues();
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Live read failed",
            detail: ex.Message,
            statusCode: 500
        );
    }
});

app.MapPost("/api/tags/reconnect", (OpcRuntimeService opc) =>
{
    opc.Reconnect();
    return Results.Ok(new { success = true, message = "OPC client reconnected." });
});



app.MapGet("/api/page2/tags", () =>
{
    var result = page2Tags.Select(tag => new TagData
    {
        TagName = tag,
        Value = "",
        Quality = "",
        Timestamp = "",
        Error = null
    }).ToList();

    return Results.Ok(result);
});

app.MapGet("/api/page2/live", (OpcRuntimeService opc) =>
{
    var result = page2Tags.Select(tag =>
    {
        try
        {
            using var client = new EasyDAClient();
            var vtq = client.ReadItem(opc.MachineName, opc.ServerName, tag);

            return new TagData
            {
                TagName = tag,
                Value = vtq.Value?.ToString() ?? "",
                Quality = vtq.Quality?.ToString() ?? "Unknown",
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Error = null
            };
        }
        catch (Exception ex)
        {
            return new TagData
            {
                TagName = tag,
                Value = "",
                Quality = "Bad",
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Error = ex.Message
            };
        }
    }).ToList();

    return Results.Ok(result);
});

app.MapPost("/api/page2/write", (ToggleTagRequest request, OpcRuntimeService opc) =>
{
    if (string.IsNullOrWhiteSpace(request.TagName) || string.IsNullOrWhiteSpace(request.Value))
    {
        return Results.BadRequest(new
        {
            success = false,
            error = "TagName and Value are required."
        });
    }

    try
    {
        var valueToWrite = request.Value;

        var attempts = new List<(string TypeName, object Value)>
        {
            ("String", valueToWrite),
            ("Int32", int.TryParse(valueToWrite, out int i32) ? i32 : 0),
            ("Int16", short.TryParse(valueToWrite, out short s16) ? s16 : (short)0),
            ("Byte", byte.TryParse(valueToWrite, out byte b) ? b : (byte)0),
            ("Boolean", bool.TryParse(valueToWrite, out bool bval) ? bval : valueToWrite == "1"),
            ("Double", double.TryParse(valueToWrite, out double dbl) ? dbl : 0.0)
        };

        var errors = new List<object>();

        foreach (var attempt in attempts)
        {
            try
            {
                using var writeClient = new EasyDAClient();

                writeClient.WriteItemValue(
                    opc.MachineName,
                    opc.ServerName,
                    request.TagName,
                    attempt.Value
                );

                return Results.Ok(new
                {
                    success = true,
                    tagName = request.TagName,
                    value = valueToWrite,
                    writtenAs = attempt.TypeName
                });
            }
            catch (Exception ex)
            {
                errors.Add(new { typeTried = attempt.TypeName, error = ex.Message });
            }
        }

        return Results.BadRequest(new
        {
            success = false,
            tagName = request.TagName,
            value = valueToWrite,
            error = "All write attempts failed.",
            details = errors
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new
        {
            success = false,
            error = ex.Message
        });
    }
});

app.MapGet("/api/page2/trigger-status", (OpcRuntimeService opc) =>
{
    try
    {
        using var client = new EasyDAClient();
        var vtq = client.ReadItem(
            opc.MachineName,
            opc.ServerName,
            "ML_MTB_Andon.ShiftTimeSetting.Test.TRIGGER"
        );

        var raw = vtq.Value;
        var isLogging =
            raw is bool b ? b :
            string.Equals(raw?.ToString(), "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw?.ToString(), "true", StringComparison.OrdinalIgnoreCase);

        return Results.Ok(new
        {
            TriggerTag = "ML_MTB_Andon.ShiftTimeSetting.Test.TRIGGER",
            TriggerValue = raw?.ToString() ?? "",
            IsLogging = isLogging,
            Quality = vtq.Quality?.ToString() ?? "Unknown"
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});



app.MapGet("/api/opc/test-item", (string tagName, OpcRuntimeService opc) =>
{
    try
    {
        using var client = new EasyDAClient();
        var vtq = client.ReadItem(opc.MachineName, opc.ServerName, tagName);

        return Results.Ok(new
        {
            success = true,
            tagName,
            value = vtq.Value?.ToString(),
            quality = vtq.Quality?.ToString()
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new
        {
            success = false,
            tagName,
            error = ex.Message
        });
    }
});

app.Run();