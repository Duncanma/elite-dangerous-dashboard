using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace EliteDashboard.Journal;

public static class JournalFileSetLocator
{
    // FOLDERID_SavedGames
    private static readonly Guid SavedGamesFolderId = new("4C5C32FF-BB9D-43b0-B5B4-2D72E54EAAA4");

    public static string ResolveJournalDirectory(string? overridePath = null)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return overridePath;
        }

        return Path.Combine(GetSavedGamesFolder(), "Frontier Developments", "Elite Dangerous");
    }

    public static FileInfo? FindLatestJournalFile(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        return new DirectoryInfo(directory)
            .GetFiles("Journal.*.log")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static string GetSavedGamesFolder()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                return GetKnownFolderPath(SavedGamesFolderId);
            }
            catch
            {
                // Known-folder lookup can fail on locked-down systems; fall back below.
            }
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games");
    }

    [SupportedOSPlatform("windows")]
    private static string GetKnownFolderPath(Guid knownFolderId)
    {
        var hr = SHGetKnownFolderPath(knownFolderId, 0, 0, out var pathPtr);
        if (hr != 0)
        {
            throw new InvalidOperationException($"SHGetKnownFolderPath failed: 0x{hr:X8}");
        }

        try
        {
            return Marshal.PtrToStringUni(pathPtr) ?? throw new InvalidOperationException("SHGetKnownFolderPath returned a null path.");
        }
        finally
        {
            Marshal.FreeCoTaskMem(pathPtr);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, nint hToken, out nint pszPath);
}
