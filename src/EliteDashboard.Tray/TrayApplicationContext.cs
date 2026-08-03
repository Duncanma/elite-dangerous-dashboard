using System.Diagnostics;

namespace EliteDashboard.Tray;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly HostProcessManager _hostManager;
    private readonly ToolStripMenuItem _statusItem;

    public TrayApplicationContext()
    {
        _hostManager = new HostProcessManager();

        var menu = new ContextMenuStrip();
        _statusItem = new ToolStripMenuItem("Starting…") { Enabled = false };
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Open Dashboard", null, (_, _) => OpenDashboard());
        menu.Items.Add("Show LAN URL / QR Code", null, (_, _) => ShowQrCode());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Restart", null, (_, _) => Restart());
        menu.Items.Add("Open Logs", null, (_, _) => OpenLogs());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());

        _trayIcon = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true,
            Text = "Elite Dangerous Dashboard",
        };
        _trayIcon.DoubleClick += (_, _) => OpenDashboard();

        StartHost();
    }

    private void StartHost()
    {
        try
        {
            _hostManager.Start();
            _statusItem.Text = $"Running on port {HostProcessManager.Port}";
        }
        catch (Exception ex)
        {
            _statusItem.Text = "Failed to start";
            _trayIcon.ShowBalloonTip(5000, "Elite Dangerous Dashboard", $"Could not start the dashboard: {ex.Message}", ToolTipIcon.Error);
        }
    }

    private string GetUrl()
    {
        var host = LanAddress.Resolve() ?? "localhost";
        return $"http://{host}:{HostProcessManager.Port}";
    }

    private void OpenDashboard()
    {
        Process.Start(new ProcessStartInfo(GetUrl()) { UseShellExecute = true });
    }

    private void ShowQrCode()
    {
        new QrCodeForm(GetUrl()).Show();
    }

    private void Restart()
    {
        _statusItem.Text = "Restarting…";
        _hostManager.Restart();
        _statusItem.Text = $"Running on port {HostProcessManager.Port}";
    }

    private void OpenLogs()
    {
        Process.Start(new ProcessStartInfo(_hostManager.LogFilePath) { UseShellExecute = true });
    }

    private void ExitApplication()
    {
        _hostManager.Dispose();
        _trayIcon.Visible = false;
        Application.Exit();
    }
}
