# Deploying Elite Dangerous Dashboard

Three scripts, run in order, all from PowerShell:

1. **`publish.ps1`** (no elevation needed) — builds the frontend, copies it into the Host's `wwwroot`, and publishes both `EliteDashboard.Host` and `EliteDashboard.Tray` into `../publish/`. Re-run this any time you update the app.
2. **`install.ps1`** (run as Administrator) — registers a Scheduled Task that launches the tray app when *you* log in (not a Windows Service — the journal lives under your user profile, and a service running as LocalSystem wouldn't be able to see `Saved Games` without extra configuration), and opens a Windows Firewall rule so other devices on your LAN can reach it. This changes standing configuration on your PC, so it's a separate, deliberate step from publishing.
3. **`uninstall.ps1`** (run as Administrator) — removes the Scheduled Task and firewall rule. Doesn't touch your app files, database, or journal data.

## After installing

- The tray icon (right-click for a menu) lets you open the dashboard in your browser, show a QR code + URL for connecting from your phone/tablet, restart the backend, or view logs.
- Your data lives in `%LOCALAPPDATA%\EliteDashboard\` (`dashboard.db`, `logs\host.log`, cached ship images) - independent of wherever you published the app to.
- Access from another device on the same network at `http://<this-PC's-LAN-IP>:5080` (shown in the tray's QR code). A static DHCP reservation on your router keeps that IP from changing.

## Manual run (no install)

You don't need to run `install.ps1` at all — just double-click `EliteDashboard.Tray.exe` in the publish folder any time you want the dashboard running.
