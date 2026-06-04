[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained,
    [string]$InstallRoot = "$env:ProgramFiles\ScapeSwitcher",
    [string]$ServiceName = "ScapeSwitcherAgent",
    [string]$DisplayName = "ScapeSwitcher Agent",
    [string]$TargetEndpointName = "Fractal Scape Dongle",
    [int]$PollIntervalMs = 2000
)

$ErrorActionPreference = "Stop"

function Assert-Admin {
    $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "This script must be run from an elevated PowerShell (Run as Administrator)."
    }
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [string[]]$Arguments = @()
    )

    Write-Host "> $FilePath $($Arguments -join ' ')"
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $($LASTEXITCODE): $FilePath $($Arguments -join ' ')"
    }
}

Assert-Admin

if ($PollIntervalMs -lt 250) {
    throw "PollIntervalMs must be >= 250."
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $scriptRoot "ScapeSwitcher.Agent.csproj"
if (-not (Test-Path $projectPath)) {
    throw "Project file not found at $projectPath"
}

$publishDir = Join-Path $InstallRoot "app"
$appExe = Join-Path $publishDir "ScapeSwitcher.Agent.exe"
$selfContainedValue = "false"
if ($SelfContained.IsPresent) {
    $selfContainedValue = "true"
}

Write-Host "Installing $DisplayName as Windows service '$ServiceName'"
Write-Host "Project: $projectPath"
Write-Host "Install root: $InstallRoot"

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

$publishArgs = @(
    "publish",
    $projectPath,
    "-c", $Configuration,
    "-r", $Runtime,
    "--output", $publishDir,
    "--self-contained", $selfContainedValue
)
Invoke-Checked -FilePath "dotnet" -Arguments $publishArgs

if (-not (Test-Path $appExe)) {
    throw "Published executable not found: $appExe"
}

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($null -ne $existing) {
    Write-Host "Service already exists. Stopping and deleting old service first..."
    & sc.exe stop $ServiceName | Out-Null
    Start-Sleep -Seconds 1
    & sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 1
}

New-Service -Name $ServiceName -BinaryPathName $appExe -DisplayName $DisplayName -StartupType Automatic
Set-Service -Name $ServiceName -StartupType Automatic

& sc.exe description $ServiceName "Switches default Windows audio device to Fractal dongle when headset connects."
& sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/5000/restart/5000 | Out-Null

[Environment]::SetEnvironmentVariable("TARGET_ENDPOINT_NAME", $TargetEndpointName, "Machine")
[Environment]::SetEnvironmentVariable("POLL_INTERVAL_MS", $PollIntervalMs.ToString(), "Machine")

& sc.exe start $ServiceName
if ($LASTEXITCODE -ne 0) {
    throw "Service created but failed to start: $ServiceName"
}

Write-Host ""
Write-Host "Install complete."
Write-Host "Service: $ServiceName"
Write-Host "Executable: $appExe"
Write-Host "TARGET_ENDPOINT_NAME=$TargetEndpointName"
Write-Host "POLL_INTERVAL_MS=$PollIntervalMs"
