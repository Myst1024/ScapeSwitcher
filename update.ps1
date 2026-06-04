[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained,
    [string]$InstallRoot = "$env:ProgramFiles\ScapeSwitcher",
    [string]$ServiceName = "ScapeSwitcherAgent"
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

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $scriptRoot "ScapeSwitcher.Agent.csproj"
$publishDir = Join-Path $InstallRoot "app"
$selfContainedValue = "false"
if ($SelfContained.IsPresent) {
    $selfContainedValue = "true"
}

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($null -eq $existing) {
    throw "Service '$ServiceName' does not exist. Run install.ps1 first."
}

Write-Host "Stopping service $ServiceName..."
& sc.exe stop $ServiceName | Out-Null
Start-Sleep -Seconds 1

$publishArgs = @(
    "publish",
    $projectPath,
    "-c", $Configuration,
    "-r", $Runtime,
    "--output", $publishDir,
    "--self-contained", $selfContainedValue
)
Invoke-Checked -FilePath "dotnet" -Arguments $publishArgs

Write-Host "Starting service $ServiceName..."
& sc.exe start $ServiceName | Out-Null

Write-Host "Update complete."
