using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScapeSwitcher.Agent.Audio;
using ScapeSwitcher.Agent.Hid;

namespace ScapeSwitcher.Agent;

public sealed class ScapeMonitorWorker(
    ILogger<ScapeMonitorWorker> logger,
    AudioDeviceSwitcher audioSwitcher,
    FractalDongleMonitor monitor) : BackgroundService
{
    private readonly ILogger<ScapeMonitorWorker> _logger = logger;
    private readonly AudioDeviceSwitcher _audioSwitcher = audioSwitcher;
    private readonly FractalDongleMonitor _monitor = monitor;

    private string _targetEndpointName = "Fractal Scape Dongle";
    private TimeSpan _pollInterval = TimeSpan.FromSeconds(2);
    private string? _fallbackEndpointId;
    private string? _fallbackEndpointName;
    private string? _activeDongleEndpointId;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LoadSettingsFromEnvironment();

        _logger.LogInformation("ScapeSwitcher worker starting. Target endpoint name contains: {Name}", _targetEndpointName);
        _logger.LogInformation("Poll interval: {PollMs}ms", _pollInterval.TotalMilliseconds);

        _monitor.HeadsetConnectionChanged += OnHeadsetConnectionChanged;

        try
        {
            await _monitor.RunAsync(_pollInterval, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
        finally
        {
            _monitor.HeadsetConnectionChanged -= OnHeadsetConnectionChanged;
            _monitor.Dispose();
            _logger.LogInformation("ScapeSwitcher worker stopped.");
        }
    }

    private void OnHeadsetConnectionChanged(object? sender, bool connected)
    {
        _logger.LogInformation("Headset connected state changed: {Connected}", connected);

        if (connected)
        {
            HandleConnected();
            return;
        }

        HandleDisconnected();
    }

    private void HandleConnected()
    {
        if (_audioSwitcher.TryGetDefaultRenderEndpoint(out var currentId, out var currentName, out var currentError))
        {
            if (_activeDongleEndpointId is null || !string.Equals(currentId, _activeDongleEndpointId, StringComparison.OrdinalIgnoreCase))
            {
                _fallbackEndpointId = currentId;
                _fallbackEndpointName = currentName;
                _logger.LogInformation("Captured fallback output device: {Device}", currentName);
            }
        }
        else
        {
            _logger.LogWarning("Unable to read current default endpoint before switch: {Error}", currentError);
        }

        var switched = _audioSwitcher.TrySwitchDefaultRenderEndpointByName(
            _targetEndpointName,
            out var dongleEndpointId,
            out var selectedDeviceName,
            out var switchError);

        if (!switched)
        {
            _logger.LogWarning("Failed to switch output on connect: {Error}", switchError);
            return;
        }

        _activeDongleEndpointId = dongleEndpointId;
        _logger.LogInformation("Default output switched to dongle endpoint: {Device}", selectedDeviceName);
    }

    private void HandleDisconnected()
    {
        if (string.IsNullOrWhiteSpace(_fallbackEndpointId))
        {
            _logger.LogInformation("No fallback endpoint stored; nothing to restore on disconnect.");
            _activeDongleEndpointId = null;
            return;
        }

        if (_audioSwitcher.TrySwitchDefaultRenderEndpointById(_fallbackEndpointId, out var error))
        {
            _logger.LogInformation("Restored fallback output device: {Device}", _fallbackEndpointName ?? _fallbackEndpointId);
        }
        else
        {
            _logger.LogWarning("Failed to restore fallback output device: {Error}", error);
        }

        _activeDongleEndpointId = null;
        _fallbackEndpointId = null;
        _fallbackEndpointName = null;
    }

    private void LoadSettingsFromEnvironment()
    {
        _targetEndpointName = Environment.GetEnvironmentVariable("TARGET_ENDPOINT_NAME") ?? "Fractal Scape Dongle";

        var pollMsRaw = Environment.GetEnvironmentVariable("POLL_INTERVAL_MS");
        if (int.TryParse(pollMsRaw, out var parsedPollMs) && parsedPollMs >= 250)
        {
            _pollInterval = TimeSpan.FromMilliseconds(parsedPollMs);
        }
    }
}
