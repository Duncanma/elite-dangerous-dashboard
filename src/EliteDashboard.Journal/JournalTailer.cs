using System.Text;
using Microsoft.Extensions.Logging;

namespace EliteDashboard.Journal;

public sealed record JournalLine(string FilePath, long LineNumber, string Json);

/// <summary>
/// Tails the current Elite Dangerous journal file: replays it from the start
/// (journals are self-contained per session/part - Fileheader/LoadGame/etc are
/// re-asserted at the top of every file), then keeps emitting new lines as the
/// game appends them, following session/part rotation to a newer file.
///
/// FileSystemWatcher is used purely to reduce latency; a periodic poll is the
/// source of truth (the watcher is known to silently miss events under load),
/// so every code path funnels through <see cref="Tick"/>.
/// </summary>
public sealed class JournalTailer : IDisposable
{
    private readonly JournalOptions _options;
    private readonly ILogger<JournalTailer> _logger;
    private readonly Lock _lock = new();

    private FileSystemWatcher? _watcher;
    private System.Threading.Timer? _pollTimer;
    private string? _currentFile;
    private long _offset;
    private string _partialLine = string.Empty;
    private long _lineNumber;
    private bool _started;

    public event Action<JournalLine>? LineRead;

    public JournalTailer(JournalOptions options, ILogger<JournalTailer> logger)
    {
        _options = options;
        _logger = logger;
    }

    public string JournalDirectory => JournalFileSetLocator.ResolveJournalDirectory(_options.JournalPathOverride);

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _logger.LogInformation("Starting journal tailer against {Directory}", JournalDirectory);

        TrySetUpWatcher();

        // Fire immediately, then on the configured interval.
        _pollTimer = new System.Threading.Timer(_ => Tick(), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(_options.PollIntervalMs));
    }

    public void Stop()
    {
        _started = false;
        _pollTimer?.Dispose();
        _pollTimer = null;
        _watcher?.Dispose();
        _watcher = null;
    }

    private void TrySetUpWatcher()
    {
        try
        {
            var dir = JournalDirectory;
            if (!Directory.Exists(dir))
            {
                _logger.LogWarning("Journal directory {Directory} does not exist yet; will retry via polling", dir);
                return;
            }

            _watcher = new FileSystemWatcher(dir, "Journal.*.log")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                InternalBufferSize = 64 * 1024,
            };
            _watcher.Changed += (_, _) => Tick();
            _watcher.Created += (_, _) => Tick();
            _watcher.Error += (_, e) => _logger.LogWarning(e.GetException(), "FileSystemWatcher error; relying on poll fallback");
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not set up FileSystemWatcher; relying entirely on poll fallback");
        }
    }

    // Internal (not private) so tests can drive a tick synchronously instead
    // of waiting on the poll timer.
    internal void Tick()
    {
        lock (_lock)
        {
            try
            {
                var dir = JournalDirectory;
                var latest = JournalFileSetLocator.FindLatestJournalFile(dir);
                if (latest is null)
                {
                    return;
                }

                if (_currentFile is null || !string.Equals(latest.FullName, _currentFile, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Switching journal tail target to {File}", latest.FullName);
                    _currentFile = latest.FullName;
                    _offset = 0;
                    _partialLine = string.Empty;
                    _lineNumber = 0;

                    // The watcher was created before this file existed (or before the
                    // directory existed) - make sure we're watching now.
                    if (_watcher is null)
                    {
                        TrySetUpWatcher();
                    }
                }

            ReadNewData();
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "Transient IO error while tailing journal; will retry next tick");
        }
        catch (Exception ex)
        {
            // Anything else unexpected (a surprise from the filesystem, a bug
            // in this class) must not kill the poll timer either - an
            // exception escaping a timer callback terminates the process.
            _logger.LogWarning(ex, "Unexpected error while tailing journal; will retry next tick");
        }
        }
    }

    private void ReadNewData()
    {
        if (_currentFile is null)
        {
            return;
        }

        using var stream = new FileStream(_currentFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        if (stream.Length < _offset)
        {
            // Should not happen for journals (append-only), but guard against it.
            _logger.LogWarning("Journal file {File} shrank unexpectedly; restarting from the beginning", _currentFile);
            _offset = 0;
            _partialLine = string.Empty;
            _lineNumber = 0;
        }

        if (stream.Length == _offset)
        {
            return;
        }

        stream.Seek(_offset, SeekOrigin.Begin);
        using var reader = new StreamReader(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var newText = reader.ReadToEnd();
        _offset = stream.Position;

        var combined = _partialLine + newText;
        var lines = combined.Split('\n');

        // The final element is either empty (input ended exactly on a newline)
        // or a partial line the game hasn't finished writing yet - buffer it.
        for (var i = 0; i < lines.Length - 1; i++)
        {
            var raw = lines[i];
            if (JournalLineNumbering.IsBlankPhysicalLine(raw))
            {
                continue;
            }

            var line = JournalLineNumbering.NormalizeEmittedLine(raw);

            _lineNumber++;
            try
            {
                LineRead?.Invoke(new JournalLine(_currentFile, _lineNumber, line));
            }
            catch (Exception ex)
            {
                // Subscribers (parser, reconciler) run synchronously here,
                // ultimately from the poll timer - an escaping exception
                // would take down the whole host. One malformed/unexpected
                // line must not kill the tail: log it and move on.
                _logger.LogError(ex, "Failed to process journal line {LineNumber} in {File}; skipping it", _lineNumber, _currentFile);
            }
        }

        _partialLine = lines[^1];
    }

    public void Dispose()
    {
        Stop();
    }
}
