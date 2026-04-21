using System.Text.Json;
using backend.Models;
using OpcLabs.EasyOpc.DataAccess;

namespace backend.Services;

public class OpcRuntimeService : IDisposable
{
    private readonly ILogger<OpcRuntimeService> _logger;
    private readonly OpcStore _store;
    private readonly OpcTagConfig _config;
    private readonly string _machineName;
    private readonly object _sync = new();

    private EasyDAClient? _client;
    private int _consecutiveReadFailures = 0;
    private bool _isReconnecting = false;

    private const int MaxReadFailuresBeforeReconnect = 3;

    public OpcRuntimeService(
        ILogger<OpcRuntimeService> logger,
        OpcStore store,
        IWebHostEnvironment env)
    {
        _logger = logger;
        _store = store;

        var jsonPath = Path.Combine(env.ContentRootPath, "tags.json");
        var json = File.ReadAllText(jsonPath);

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        _config = JsonSerializer.Deserialize<OpcTagConfig>(json, jsonOptions)
                 ?? throw new Exception("Invalid tags.json configuration.");

        if (string.IsNullOrWhiteSpace(_config.ServerName) || _config.Tags.Count == 0)
            throw new Exception("Invalid tags.json configuration.");

        _machineName = string.IsNullOrWhiteSpace(_config.MachineName)
            ? ""
            : _config.MachineName;

        InitializeSubscriptions();
    }

    public IReadOnlyList<string> Tags => _config.Tags;
    public string ServerName => _config.ServerName;
    public string MachineName => _machineName;

    private void InitializeSubscriptions()
    {
        lock (_sync)
        {
            DisposeClientOnly();

            _client = new EasyDAClient();
            _client.ItemChanged += OnItemChanged;

            foreach (var tag in _config.Tags)
            {
                _store.Set(new TagData
                {
                    TagName = tag,
                    Value = "0",
                    Quality = "Initializing",
                    Timestamp = null,
                    Error = null
                });

                _client.SubscribeItem(
                    _machineName,
                    _config.ServerName,
                    tag,
                    _config.UpdateRate
                );
            }

            _consecutiveReadFailures = 0;
            _logger.LogInformation(
                "OPC subscriptions initialized for {Count} tags.",
                _config.Tags.Count
            );
        }
    }

    private void OnItemChanged(object? sender, EventArgs e)
    {
        try
        {
            dynamic args = e;

            string itemId = Convert.ToString(args.Arguments?.ItemDescriptor?.ItemId) ?? "";
            if (string.IsNullOrWhiteSpace(itemId))
                return;

            bool succeeded = Convert.ToBoolean(args.Succeeded);

            if (succeeded)
            {
                string? value = NormalizeValue(args.Vtq?.Value);
                string? quality = Convert.ToString(args.Vtq?.Quality);

                string? timestamp = null;
                if (args.Vtq?.Timestamp != null)
                {
                    timestamp = Convert
                        .ToDateTime(args.Vtq.Timestamp)
                        .ToString("yyyy-MM-dd h:mm:ss tt");
                }

                _store.Set(new TagData
                {
                    TagName = itemId,
                    Value = value,
                    Quality = quality,
                    Timestamp = timestamp,
                    Error = null
                });
            }
            else
            {
                string errorMessage = Convert.ToString(args.ErrorMessageBrief) ?? "Unknown OPC error";

                _store.Set(new TagData
                {
                    TagName = itemId,
                    Value = null,
                    Quality = "Error",
                    Timestamp = null,
                    Error = errorMessage
                });

                _logger.LogWarning(
                    "OPC subscription error for tag {TagName}: {Error}",
                    itemId,
                    errorMessage
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled OPC ItemChanged processing error.");
        }
    }

    public List<TagData> ReadLiveValues()
    {
        var result = new List<TagData>();
        var failedReads = 0;

        try
        {
            using var readClient = new EasyDAClient();

            foreach (var tag in _config.Tags)
            {
                try
                {
                    var vtq = readClient.ReadItem(_machineName, _config.ServerName, tag);

                    result.Add(new TagData
                    {
                        TagName = tag,
                        Value = NormalizeValue(vtq.Value),
                        Quality = vtq.Quality.ToString(),
                        Timestamp = vtq.Timestamp.ToString("yyyy-MM-dd h:mm:ss tt"),
                        Error = null
                    });
                }
                catch (Exception ex)
                {
                    failedReads++;

                    result.Add(new TagData
                    {
                        TagName = tag,
                        Value = null,
                        Quality = "Error",
                        Timestamp = null,
                        Error = ex.Message
                    });

                    string tagName = tag ?? "unknown";
                    _logger.LogWarning(ex, "Live read failed for tag {TagName}", tagName);
                }
            }

            if (failedReads == 0)
            {
                _consecutiveReadFailures = 0;
            }
            else
            {
                RegisterReadFailure();
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Global live read failure.");
            RegisterReadFailure();

            return _config.Tags.Select(tag => new TagData
            {
                TagName = tag,
                Value = null,
                Quality = "Error",
                Timestamp = null,
                Error = ex.Message
            }).ToList();
        }
    }

    public void Reconnect()
    {
        lock (_sync)
        {
            if (_isReconnecting)
                return;

            _isReconnecting = true;
        }

        try
        {
            _logger.LogWarning("Reconnecting OPC client and resubscribing all tags.");
            InitializeSubscriptions();
        }
        finally
        {
            lock (_sync)
            {
                _isReconnecting = false;
            }
        }
    }

    private void RegisterReadFailure()
    {
        _consecutiveReadFailures++;

        if (_consecutiveReadFailures >= MaxReadFailuresBeforeReconnect)
        {
            _logger.LogWarning(
                "Read failures reached threshold {Threshold}. Triggering reconnect.",
                MaxReadFailuresBeforeReconnect
            );

            Reconnect();
        }
    }

    private void DisposeClientOnly()
    {
        if (_client != null)
        {
            try
            {
                _client.ItemChanged -= OnItemChanged;
                _client.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while disposing OPC client.");
            }

            _client = null;
        }
    }

    public void Dispose()
    {
        DisposeClientOnly();
    }

    private static string? NormalizeValue(object? value)
    {
        if (value == null)
            return null;

        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
    }
}