<#
.SYNOPSIS
    Removes the Scheduled Task and firewall rule created by install.ps1.
    Does not touch the app files, your SQLite database, or your journal data.
#>
param(
    [int]$Port = 5080
)

$ErrorActionPreference = "Stop"

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this from an elevated (Administrator) PowerShell prompt."
}

$taskName = "EliteDashboardDashboard"
$ruleName = "Elite Dangerous Dashboard"

Write-Host "==> Removing Scheduled Task '$taskName'" -ForegroundColor Cyan
Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue

Write-Host "==> Removing firewall rule '$ruleName'" -ForegroundColor Cyan
Remove-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Uninstalled. Your dashboard.db and journal data are untouched." -ForegroundColor Green
Write-Host "If the tray icon/host are still running, right-click the tray icon and choose Exit." -ForegroundColor Yellow
