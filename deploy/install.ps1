<#
.SYNOPSIS
    Registers a logon-triggered Scheduled Task for the dashboard and opens a
    Windows Firewall rule for its port. Run from an elevated PowerShell prompt.

.DESCRIPTION
    Deliberately NOT a classic Windows Service: the journal lives under this
    user's profile (%USERPROFILE%\Saved Games), and a LocalSystem-account
    service would need extra "log on as this user" configuration to see it.
    A logon-triggered Scheduled Task runs in your own session instead, so it
    just works.

    This only touches:
      - One Scheduled Task named "EliteDashboardDashboard" (logon trigger, current user)
      - One Windows Firewall inbound rule named "Elite Dangerous Dashboard" (Private profile, TCP 5080)
    Run deploy/uninstall.ps1 to remove both.

.PARAMETER InstallDir
    Folder containing the published app (output of deploy/publish.ps1).
    Defaults to ../publish next to this script.

.PARAMETER Port
    Must match the port EliteDashboard.Host listens on. Defaults to 5080.
#>
param(
    [string]$InstallDir = (Join-Path $PSScriptRoot "..\publish"),
    [int]$Port = 5080
)

$ErrorActionPreference = "Stop"

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this from an elevated (Administrator) PowerShell prompt - both the Scheduled Task and firewall rule require it."
}

$InstallDir = Resolve-Path $InstallDir
$trayExe = Join-Path $InstallDir "EliteDashboard.Tray.exe"
if (-not (Test-Path $trayExe)) {
    throw "EliteDashboard.Tray.exe not found in $InstallDir. Run deploy/publish.ps1 first."
}

$taskName = "EliteDashboardDashboard"
$ruleName = "Elite Dangerous Dashboard"

Write-Host "==> Registering Scheduled Task '$taskName' (logon trigger, user: $env:USERNAME)" -ForegroundColor Cyan
$action = New-ScheduledTaskAction -Execute $trayExe -WorkingDirectory $InstallDir
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings -Force | Out-Null

Write-Host "==> Opening firewall for TCP $Port (Private networks only)" -ForegroundColor Cyan
Remove-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow -Profile Private | Out-Null

Write-Host ""
Write-Host "Installed. The dashboard will start automatically next time you log in." -ForegroundColor Green
Write-Host "To start it right now: `"$trayExe`"" -ForegroundColor Green
Write-Host "To remove this later: deploy/uninstall.ps1" -ForegroundColor Yellow
