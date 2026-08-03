using Microsoft.Extensions.Logging;

namespace EliteDashboard.Journal;

/// <summary>
/// Watches one overwritten-in-place snapshot file (e.g. Status.json) and
/// raises an event with the freshly parsed content whenever it changes.
/// Like JournalTailer, a poll timer is the source of truth and the
/// FileSystemWatcher just reduces latency - it's known to miss events under load.
/// </summary>
public sealed class SnapshotWatcher<T> : IDisposable where T : class
{
    private readonly string _directory;
    private readonly string _fileName;
    private readonly int _pollIntervalMs;
    private readonly ILogger _logger;
    private FileSystemWatcher? _watcher;
    private System.Threading.Timer? _pollTimer;
    private readonly Lock _lock = new();

    public event Action<T>? Changed;

    public SnapshotWatcher(string directory, string fileName, ILogger logger, int pollIntervalMs = 1000)
    {
        _directory = directory;
        _fileName = fileName;
        _logger = logger;
        _pollIntervalMs = pollIntervalMs;
    }

    public void Start()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                _watcher = new FileSystemWatcher(_directory, _fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                };
                _watcher.Changed += (_, _) => Tick();
                _watcher.Error += (_, e) => _logger.LogWarning(e.GetException(), "SnapshotWatcher for {File} error; relying on poll fallback", _fileName);
                _watcher.EnableRaisingEvents = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not set up FileSystemWatcher for {File}; relying entirely on poll fallback", _fileName);
        }

        _pollTimer = new System.Threading.Timer(_ => Tick(), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(_pollIntervalMs));
    }

    private DateTime _lastReadUtc;

    private void Tick()
    {
        lock (_lock)
        {
            var path = Path.Combine(_directory, _fileName);
            if (!File.Exists(path))
            {
                return;
            }

            var lastWrite = File.GetLastWriteTimeUtc(path);
            if (lastWrite == _lastReadUtc)
            {
                return;
            }

            var value = SnapshotFileReader.TryRead<T>(path);
            if (value is null)
            {
                return;
            }

            _lastReadUtc = lastWrite;
            Changed?.Invoke(value);
        }
    }

    public void Dispose()
    {
        _pollTimer?.Dispose();
        _watcher?.Dispose();
    }
}
