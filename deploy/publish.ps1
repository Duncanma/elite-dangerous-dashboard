<#
.SYNOPSIS
    Builds the frontend and publishes the Host + Tray apps into one folder -
    a single deployable directory you can run from anywhere.

.DESCRIPTION
    1. npm run build (frontend) -> web/dist
    2. Copies web/dist into the Host's wwwroot so Kestrel serves the SPA
       and the API from the same port (see Program.cs UseStaticFiles/MapFallbackToFile).
    3. dotnet publish both EliteDashboard.Host and EliteDashboard.Tray as
       self-contained-less (framework-dependent) single-folder output.

    Run this from the repo root, or anywhere - it resolves paths relative to
    this script's own location.

.PARAMETER OutputDir
    Where to put the published app. Defaults to ./publish next to this script's repo root.
#>
param(
    [string]$OutputDir = (Join-Path $PSScriptRoot "..\publish")
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$webDir = Join-Path $repoRoot "web"
$hostProject = Join-Path $repoRoot "src\EliteDashboard.Host\EliteDashboard.Host.csproj"
$trayProject = Join-Path $repoRoot "src\EliteDashboard.Tray\EliteDashboard.Tray.csproj"
$wwwroot = Join-Path $repoRoot "src\EliteDashboard.Host\wwwroot"

Write-Host "==> Building frontend" -ForegroundColor Cyan
Push-Location $webDir
try {
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "npm run build failed" }
} finally {
    Pop-Location
}

Write-Host "==> Copying web/dist into Host/wwwroot" -ForegroundColor Cyan
if (Test-Path $wwwroot) { Remove-Item $wwwroot -Recurse -Force }
Copy-Item (Join-Path $webDir "dist") $wwwroot -Recurse

Write-Host "==> Publishing Host" -ForegroundColor Cyan
dotnet publish $hostProject -c Release -o $OutputDir --self-contained false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish (Host) failed" }

Write-Host "==> Publishing Tray" -ForegroundColor Cyan
dotnet publish $trayProject -c Release -o $OutputDir --self-contained false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish (Tray) failed" }

Write-Host ""
Write-Host "Published to: $OutputDir" -ForegroundColor Green
Write-Host "Run EliteDashboard.Tray.exe from that folder to start the dashboard." -ForegroundColor Green
Write-Host "To auto-start at logon and open the firewall, run deploy/install.ps1 -InstallDir `"$OutputDir`" from an elevated PowerShell prompt." -ForegroundColor Yellow
