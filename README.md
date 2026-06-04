# ScapeSwitcher.Agent

Background C# agent that monitors Fractal Scape dongle HID state and switches the default Windows audio output when the headset connects.

## Behavior:

- When headset connects, app switches Windows default render endpoint to the dongle endpoint.
- Before switching, app captures current default output as fallback.
- When headset disconnects, app restores that fallback output endpoint.
- App runs as a generic host and is Windows Service compatible.

## Requirements

- .NET 10 SDK
- Windows

## Build and run

```powershell
dotnet restore

dotnet run --project .\ScapeSwitcher.Agent.csproj
```

## Run as Windows service

Publish and register service (example):

```powershell
dotnet publish .\ScapeSwitcher.Agent.csproj -c Release -r win-x64 --self-contained false

sc.exe create ScapeSwitcherAgent binPath= "C:\Path\To\publish\ScapeSwitcher.Agent.exe" start= auto
sc.exe start ScapeSwitcherAgent
```

Delete service:

```powershell
sc.exe stop ScapeSwitcherAgent
sc.exe delete ScapeSwitcherAgent
```

## Script-based install (recommended)

Run PowerShell as Administrator in the project root.

Install service:

```powershell
.\install.ps1
```

Optional install parameters:

```powershell
.\install.ps1 -TargetEndpointName "Fractal Scape Dongle" -PollIntervalMs 1500
```

Update deployed binaries:

```powershell
.\update.ps1
```

Uninstall service:

```powershell
.\uninstall.ps1
```

Keep machine environment variables on uninstall:

```powershell
.\uninstall.ps1 -KeepConfig
```

## Optional environment variables

- `TARGET_ENDPOINT_NAME`
  - Default: `Fractal Scape Dongle`
  - Partial case-insensitive match against active render endpoint name.
- `POLL_INTERVAL_MS`
  - Default: `2000`
  - Minimum accepted value: `250`

## Notes

- If endpoint names differ on your machine, set `TARGET_ENDPOINT_NAME`.
