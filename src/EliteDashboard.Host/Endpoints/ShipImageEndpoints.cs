namespace EliteDashboard.Host.Endpoints;

/// <summary>
/// Serves a badge image per ship. No stable, legal source of real ship
/// artwork was found automatically (EDAssets.org is a JS-rendered SPA with no
/// discoverable API; the Fandom wiki's pages are behind Cloudflare bot
/// protection, though its static asset CDN serves direct image URLs fine).
/// Real art is instead user-supplied under
/// <c>%LOCALAPPDATA%\EliteDashboard\ship-art\</c> - never checked into the
/// repo, since it's personal/possibly-not-freely-licensed and specific to
/// each install:
///
///   ship-art\by-type\{Ship type display name}.{webp|png|jpg|jpeg}   - generic art for any ship of that type
///   ship-art\by-ship\{Your ship's given name}.{webp|png|jpg|jpeg}   - a specific hull (your paint job, screenshot, etc.)
///
/// Matching is normalized (letters/digits only, case-insensitive), so
/// "Krait Mk II.webp", "krait-mk-ii.png", and "KRAITMKII.jpg" are all the same
/// key - forgiving of exact spacing/casing since these filenames are meant to
/// be hand-typed. by-ship is checked first (keyed by the ship's own name, if
/// it has one), then by-type (keyed by the ship's type display name, falling
/// back to its internal symbol for anyone who'd rather use that). Directories
/// are re-scanned on every request rather than cached - there are only ever a
/// handful of files, and it means a dropped-in file shows up immediately with
/// no restart. Anything not found in either folder falls back to a generated
/// placeholder, cached to disk: shape driven by the ship's real size class
/// (from ShipStaticSpec, sourced from coriolis-data) when known - small ships
/// get a circle, medium a rounded square, large a hexagon.
/// </summary>
public static class ShipImageEndpoints
{
    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EliteDashboard");

    private static readonly string CacheDir = Path.Combine(AppDataDir, "ship-images");
    private static readonly string ByShipDir = Path.Combine(AppDataDir, "ship-art", "by-ship");
    private static readonly string ByTypeDir = Path.Combine(AppDataDir, "ship-art", "by-type");

    private static readonly string[] AllowedExtensions = [".webp", ".png", ".jpg", ".jpeg"];

    public static void MapShipImageEndpoints(this WebApplication app)
    {
        Directory.CreateDirectory(CacheDir);
        Directory.CreateDirectory(ByShipDir);
        Directory.CreateDirectory(ByTypeDir);

        app.MapGet("/api/ships/{shipType}/image", (string shipType, string? display, string? shipName, int? sizeClass) =>
        {
            var userImage = FindUserImage(ByShipDir, shipName)
                ?? FindUserImage(ByTypeDir, display, shipType);

            if (userImage is not null)
            {
                return Results.File(userImage.Value.Path, userImage.Value.ContentType);
            }

            var normalizedType = NormalizeKey(shipType);
            var cachedPath = Path.Combine(CacheDir, $"{normalizedType}-{sizeClass ?? 0}.svg");
            if (!File.Exists(cachedPath))
            {
                File.WriteAllText(cachedPath, GeneratePlaceholderSvg(shipType, display, sizeClass ?? 0));
            }

            return Results.File(cachedPath, "image/svg+xml");
        });
    }

    /// <summary>Tries each candidate key in order against every file in <paramref name="dir"/>, first match wins.</summary>
    private static (string Path, string ContentType)? FindUserImage(string dir, params string?[] candidateKeys)
    {
        var keys = candidateKeys.Where(k => !string.IsNullOrWhiteSpace(k)).Select(NormalizeKey).Distinct().ToList();
        if (keys.Count == 0 || !Directory.Exists(dir))
        {
            return null;
        }

        foreach (var file in Directory.EnumerateFiles(dir))
        {
            var ext = Path.GetExtension(file);
            if (!AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var fileKey = NormalizeKey(Path.GetFileNameWithoutExtension(file));
            if (keys.Contains(fileKey))
            {
                return (file, ContentTypeFor(ext));
            }
        }

        return null;
    }

    private static string ContentTypeFor(string extension) => extension.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "image/webp",
    };

    /// <summary>Letters/digits only, lowercased - forgiving of spacing/punctuation/casing differences in hand-typed filenames.</summary>
    private static string NormalizeKey(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "";
        }

        var cleaned = new string(raw.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return cleaned.Length > 0 ? cleaned : "";
    }

    private static string GeneratePlaceholderSvg(string shipType, string? display, int sizeClass)
    {
        var hue = StableHash(shipType) % 360;
        var label = ShortLabel(display ?? shipType);
        var shape = sizeClass switch
        {
            1 => """<circle cx="80" cy="80" r="76" fill="url(#g)" />""",
            3 => """<polygon points="80,6 150,44 150,116 80,154 10,116 10,44" fill="url(#g)" />""",
            _ => """<rect width="160" height="160" rx="20" fill="url(#g)" />""",
        };

        return $"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 160 160" width="160" height="160" role="img" aria-label="{System.Net.WebUtility.HtmlEncode(display ?? shipType)}">
              <defs>
                <linearGradient id="g" x1="0" y1="0" x2="1" y2="1">
                  <stop offset="0%" stop-color="hsl({hue},60%,42%)" />
                  <stop offset="100%" stop-color="hsl({(hue + 40) % 360},60%,26%)" />
                </linearGradient>
              </defs>
              {shape}
              <text x="80" y="96" font-family="system-ui, sans-serif" font-size="44" font-weight="600" fill="white" text-anchor="middle">{label}</text>
            </svg>
            """;
    }

    private static string ShortLabel(string name)
    {
        var words = name.Replace('_', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length switch
        {
            0 => "?",
            1 => words[0].Length >= 2 ? words[0][..2].ToUpperInvariant() : words[0].ToUpperInvariant(),
            _ => string.Concat(words[0][0], words[1][0]).ToUpperInvariant(),
        };
    }

    /// <summary>
    /// string.GetHashCode() is randomized per-process in .NET - a stable hash
    /// is needed so the same ship type gets the same color across app restarts.
    /// </summary>
    private static int StableHash(string s)
    {
        unchecked
        {
            var hash = 23;
            foreach (var c in s)
            {
                hash = hash * 31 + c;
            }

            return Math.Abs(hash);
        }
    }
}
