using HidSharp;

namespace ScapeSwitcher.Agent.Hid;

public sealed class FractalDongleMonitor : IDisposable
{
    // Extracted from Fractal Adjust bundle:
    // VendorIDs.FRACTAL_VENDOR_ID = 14012
    // ProductIDs.DONGLE_PRODUCT_ID = 1
    // P911.REPORT_ID = 2
    // P911.DEVICE_TYPE_FACTORY_DONGLE = 17
    // P911.GET_DEVICE_STATE_CMD = 33
    private const int FractalVendorId = 14012;
    private const int DongleProductId = 1;
    private const byte ReportId = 2;
    private const byte FactoryDeviceTypeDongle = 17;
    private const byte GetDeviceStateCommand = 33;

    private HidDevice? _device;
    private HidStream? _stream;
    private bool? _lastKnownConnectionState;
    private DateTime _lastOpenFailureLogUtc = DateTime.MinValue;

    public event EventHandler<bool>? HeadsetConnectionChanged;

    public async Task RunAsync(TimeSpan pollInterval, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                EnsureOpenStream();

                if (_stream is not null && _device is not null)
                {
                    var isConnected = QueryHeadsetConnected(_device, _stream);
                    if (!_lastKnownConnectionState.HasValue || _lastKnownConnectionState.Value != isConnected)
                    {
                        _lastKnownConnectionState = isConnected;
                        HeadsetConnectionChanged?.Invoke(this, isConnected);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:O}] Monitor error: {ex.Message}");
                CloseStream();
            }

            await Task.Delay(pollInterval, cancellationToken);
        }
    }

    private void EnsureOpenStream()
    {
        if (_stream is not null && _stream.CanRead && _stream.CanWrite)
        {
            return;
        }

        CloseStream();

        var candidates = DeviceList.Local
            .GetHidDevices(FractalVendorId, DongleProductId)
            .ToList();

        foreach (var candidate in candidates)
        {
            if (!candidate.TryOpen(out var openedStream))
            {
                continue;
            }

            try
            {
                openedStream.ReadTimeout = 1500;
                openedStream.WriteTimeout = 1500;

                // Probe each collection/interface and keep the first one that supports the
                // Fractal device-state command.
                _ = QueryHeadsetConnected(candidate, openedStream);

                _device = candidate;
                _stream = openedStream;

                Console.WriteLine($"[{DateTime.Now:O}] Opened HID device VID={candidate.VendorID} PID={candidate.ProductID} Path={candidate.DevicePath}");
                return;
            }
            catch
            {
                openedStream.Dispose();
            }
        }

        var nowUtc = DateTime.UtcNow;
        if ((nowUtc - _lastOpenFailureLogUtc) > TimeSpan.FromSeconds(15))
        {
            _lastOpenFailureLogUtc = nowUtc;
            Console.WriteLine($"[{DateTime.Now:O}] Unable to open a writable Fractal dongle HID interface. Candidates found: {candidates.Count}");
        }
    }

    private static bool QueryHeadsetConnected(HidDevice device, HidStream stream)
    {
        var outputLength = Math.Max(device.GetMaxOutputReportLength(), 8);
        var output = new byte[outputLength];

        // HID output format is [reportId, payload...]
        output[0] = ReportId;
        output[1] = FactoryDeviceTypeDongle;
        output[2] = GetDeviceStateCommand;
        stream.Write(output);

        var inputLength = Math.Max(device.GetMaxInputReportLength(), 8);
        var input = new byte[inputLength];
        var bytesRead = stream.Read(input, 0, input.Length);

        if (bytesRead <= 0)
        {
            throw new IOException("No HID response received.");
        }

        var hasReportPrefix = input[0] == ReportId;
        var offset = hasReportPrefix ? 1 : 0;

        if (bytesRead <= offset + 3)
        {
            throw new IOException($"HID response too short: {bytesRead} bytes.");
        }

        // Matches Fractal app logic: headsetConnected = (responseByte3 == 1)
        return input[offset + 3] == 1;
    }

    private void CloseStream()
    {
        _stream?.Dispose();
        _stream = null;
        _device = null;
    }

    public void Dispose()
    {
        CloseStream();
    }
}
