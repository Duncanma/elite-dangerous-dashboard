using System.Text.Json;

namespace EliteDashboard.Journal;

/// <summary>
/// Reads a "snapshot" file (Status.json and friends) that the game overwrites
/// in place rather than appends to. Unlike the journal, there's no byte-offset
/// tracking - the whole file is re-read and re-parsed every time it changes.
/// A brief retry absorbs the case where we read mid-write and get a truncated
/// or invalid JSON document.
/// </summary>
public static class SnapshotFileReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static T? TryRead<T>(string path, int maxAttempts = 5, int retryDelayMs = 20) where T : class
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return JsonSerializer.Deserialize<T>(stream, Options);
            }
            catch (IOException) when (attempt == maxAttempts)
            {
                return null;
            }
            catch (JsonException) when (attempt == maxAttempts)
            {
                // Give up quietly, as documented above - this runs on a
                // FileSystemWatcher callback thread, and an unhandled
                // exception there crashes the entire process, not just this
                // read. The next Tick() will just try again on the next write.
                return null;
            }
            catch (IOException)
            {
                Thread.Sleep(retryDelayMs);
            }
            catch (JsonException)
            {
                Thread.Sleep(retryDelayMs);
            }
        }

        return null;
    }
}
