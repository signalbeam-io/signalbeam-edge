namespace SignalBeam.EdgeAgent.Host.Services;

/// <summary>
/// Manages the device registration state and credentials in memory and persistent storage.
/// </summary>
public class DeviceStateManager
{
    private readonly object _lock = new();
    private readonly string _stateFilePath;

    private Guid? _deviceId;
    private string? _apiKey;
    private string? _cloudEndpoint;
    private bool _isRegistered;

    public DeviceStateManager()
    {
        // Store state in user data directory
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "signalbeam-agent");

        Directory.CreateDirectory(dataDirectory);
        _stateFilePath = Path.Combine(dataDirectory, "device-state.json");

        LoadState();
    }

    public Guid? DeviceId
    {
        get
        {
            lock (_lock)
            {
                return _deviceId;
            }
        }
    }

    public string? ApiKey
    {
        get
        {
            lock (_lock)
            {
                return _apiKey;
            }
        }
    }

    public string? CloudEndpoint
    {
        get
        {
            lock (_lock)
            {
                return _cloudEndpoint;
            }
        }
    }

    public bool IsRegistered
    {
        get
        {
            lock (_lock)
            {
                return _isRegistered;
            }
        }
    }

    public void SetRegistrationState(Guid deviceId, string apiKey, string cloudEndpoint)
    {
        lock (_lock)
        {
            _deviceId = deviceId;
            _apiKey = apiKey;
            _cloudEndpoint = cloudEndpoint;
            _isRegistered = true;

            SaveState();
        }
    }

    public void ClearState()
    {
        lock (_lock)
        {
            _deviceId = null;
            _apiKey = null;
            _cloudEndpoint = null;
            _isRegistered = false;

            if (File.Exists(_stateFilePath))
            {
                File.Delete(_stateFilePath);
            }
        }
    }

    private void LoadState()
    {
        lock (_lock)
        {
            if (!File.Exists(_stateFilePath))
            {
                return;
            }

            try
            {
                var json = File.ReadAllText(_stateFilePath);
                var state = System.Text.Json.JsonSerializer.Deserialize<DeviceState>(json);

                if (state != null)
                {
                    _deviceId = state.DeviceId;
                    _apiKey = state.ApiKey;
                    _cloudEndpoint = state.CloudEndpoint;
                    _isRegistered = true;
                }
            }
            catch
            {
                // Ignore errors loading state, will re-register if needed
            }
        }
    }

    private void SaveState()
    {
        lock (_lock)
        {
            var state = new DeviceState
            {
                DeviceId = _deviceId!.Value,
                ApiKey = _apiKey!,
                CloudEndpoint = _cloudEndpoint!
            };

            var json = System.Text.Json.JsonSerializer.Serialize(state, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_stateFilePath, json);
        }
    }

    private class DeviceState
    {
        public Guid DeviceId { get; set; }
        public string ApiKey { get; set; } = string.Empty;
        public string CloudEndpoint { get; set; } = string.Empty;
    }
}
