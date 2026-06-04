using NAudio.CoreAudioApi;

namespace ScapeSwitcher.Agent.Audio;

public sealed class AudioDeviceSwitcher
{
    public bool TryGetDefaultRenderEndpoint(out string endpointId, out string endpointName, out string error)
    {
        endpointId = string.Empty;
        endpointName = string.Empty;
        error = string.Empty;

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            endpointId = device.ID;
            endpointName = device.FriendlyName;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TrySwitchDefaultRenderEndpointByName(string nameContains, out string selectedDeviceName, out string error)
    {
        return TrySwitchDefaultRenderEndpointByName(nameContains, out _, out selectedDeviceName, out error);
    }

    public bool TrySwitchDefaultRenderEndpointByName(string nameContains, out string selectedEndpointId, out string selectedDeviceName, out string error)
    {
        selectedEndpointId = string.Empty;
        selectedDeviceName = string.Empty;
        error = string.Empty;

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var candidates = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            var match = candidates
                .FirstOrDefault(device => device.FriendlyName.Contains(nameContains, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                error = $"No active render endpoint matched '{nameContains}'.";
                return false;
            }

            if (!TrySwitchDefaultRenderEndpointById(match.ID, out var switchError))
            {
                error = switchError;
                return false;
            }

            selectedEndpointId = match.ID;
            selectedDeviceName = match.FriendlyName;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TrySwitchDefaultRenderEndpointById(string endpointId, out string error)
    {
        error = string.Empty;

        try
        {
            var policy = new PolicyConfigClient() as IPolicyConfig;
            if (policy is null)
            {
                error = "Unable to initialize Windows audio policy config COM interface.";
                return false;
            }

            var hrConsole = policy.SetDefaultEndpoint(endpointId, ERole.eConsole);
            var hrMultimedia = policy.SetDefaultEndpoint(endpointId, ERole.eMultimedia);
            var hrCommunications = policy.SetDefaultEndpoint(endpointId, ERole.eCommunications);

            if (hrConsole != 0 || hrMultimedia != 0 || hrCommunications != 0)
            {
                error = $"SetDefaultEndpoint failed. HRESULTs: console={hrConsole}, multimedia={hrMultimedia}, comms={hrCommunications}";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
