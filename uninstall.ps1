[CmdletBinding()]
param(
    [string]$InstallRoot = "$env:ProgramFiles\ScapeSwitcher",
    [string]$ServiceName = "ScapeSwitcherAgent",
    [switch]$KeepConfig
)

$ErrorActionPreference = "Stop"

function Assert-Admin {
    $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "This script must be run from an elevated PowerShell (Run as Administrator)."
    }
}

Assert-Admin

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($null -ne $existing) {
    Write-Host "Stopping service $ServiceName..."
    & sc.exe stop $ServiceName | Out-Null
    Start-Sleep -Seconds 1

    Write-Host "Deleting service $ServiceName..."
    & sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 1
}
else {
    Write-Host "Service $ServiceName not found."
}

if (-not $KeepConfig.IsPresent) {
    [Environment]::SetEnvironmentVariable("TARGET_ENDPOINT_NAME", $null, "Machine")
    [Environment]::SetEnvironmentVariable("POLL_INTERVAL_MS", $null, "Machine")
    Write-Host "Removed machine-level environment variables for ScapeSwitcher."
}

if (Test-Path $InstallRoot) {
    Write-Host "Removing install directory $InstallRoot"
    Remove-Item -Path $InstallRoot -Recurse -Force
}

Write-Host "Uninstall complete."
