using System.Diagnostics;

namespace EliteDashboard.Tray;

/// <summary>
/// Launches and monitors EliteDashboard.Host.exe as a child process. Kept as
/// a separate process (not merged into the tray app) so the Host stays a
/// plain, headless-testable ASP.NET Core app - the tray is just a thin,
/// swappable launcher/status-icon around it.
/// </summary>
public sealed class HostProcessManager : IDisposable
{
    public const int Port = 5080;

    private readonly string _hostExePath;
    private readonly string _logFilePath;
    private Process? _process;
    private StreamWriter? _logWriter;

    public bool IsRunning => _process is { HasExited: false };

    public HostProcessManager()
    {
        _hostExePath = ResolveHostExePath();

        var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EliteDashboard", "logs");
        Directory.CreateDirectory(logDir);
        _logFilePath = Path.Combine(logDir, "host.log");
    }

    public string LogFilePath => _logFilePath;

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _logWriter = new StreamWriter(File.Open(_logFilePath, FileMode.Create, FileAccess.Write, FileShare.Read)) { AutoFlush = true };

        var startInfo = new ProcessStartInfo
        {
            FileName = _hostExePath,
            // The Generic Host resolves ContentRootPath (and therefore
            // appsettings.json) from the current working directory, not the
            // executable's own directory - without this, the Host inherits
            // whatever cwd the Tray app happens to be running from and can
            // silently fall back to hardcoded defaults (e.g. full EF Core
            // SQL command logging, which floods the log file in minutes).
            WorkingDirectory = Path.GetDirectoryName(_hostExePath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.EnvironmentVariables["ASPNETCORE_URLS"] = $"http://0.0.0.0:{Port}";

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, e) => { if (e.Data is not null) _logWriter?.WriteLine(e.Data); };
        _process.ErrorDataReceived += (_, e) => { if (e.Data is not null) _logWriter?.WriteLine(e.Data); };
        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    public void Stop()
    {
        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(5000);
        }

        _process?.Dispose();
        _process = null;
        _logWriter?.Dispose();
        _logWriter = null;
    }

    public void Restart()
    {
        Stop();
        Start();
    }

    private static string ResolveHostExePath()
    {
        // Dev-mode override, since a full publish output isn't produced by `dotnet build`.
        var overridePath = Environment.GetEnvironmentVariable("ELITEDASHBOARD_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
        {
            return overridePath;
        }

        // Production layout: both apps published side-by-side.
        var sibling = Path.Combine(AppContext.BaseDirectory, "EliteDashboard.Host.exe");
        if (File.Exists(sibling))
        {
            return sibling;
        }

        throw new FileNotFoundException(
            "Could not find EliteDashboard.Host.exe. Set ELITEDASHBOARD_HOST_PATH for dev testing, " +
            "or publish both apps to the same output directory for production use.");
    }

    public void Dispose() => Stop();
}
