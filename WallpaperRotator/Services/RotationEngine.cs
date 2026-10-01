using WallpaperRotator.Models;

namespace WallpaperRotator.Services;

public class RotationResult
{
    public WallhavenWallpaper Wallpaper { get; init; } = null!;
    public string FilePath { get; init; } = "";
    public int ExtraCount { get; init; }
}

public class RotationEngine
{
    private readonly WallhavenClient _client = new();
    private readonly string _cacheFolder = CacheService.Folder;

    public string CacheFolder => _cacheFolder;
    public bool CachePruneEnabled { get; set; } = true;
    public int CacheMaxFiles { get; set; } = CacheService.DefaultMaxFiles;
    public long CacheMaxBytes { get; set; } = CacheService.DefaultMaxBytes;
    public bool MultiMonitor { get; set; }
    public int CollectionId { get; set; }
    public string ApiKey { get; set; } = "";

    public async Task<RotationResult?> RotateAsync(
        WallhavenQuery q, WallpaperFit fit, CancellationToken ct = default)
    {
        _client.ApiKey = ApiKey;
        LogService.Info($"Rotate start: source={(CollectionId != 0 ? $"collection {CollectionId}" : $"search '{q.Tag}'")} " +
                        $"sorting={q.Sorting} {(q.Exact ? "exactly" : "atleast")}={q.Width}x{q.Height} " +
                        $"ratio={(string.IsNullOrEmpty(q.Ratio) ? "-" : q.Ratio)} color={(string.IsNullOrEmpty(q.ColorHex) ? "-" : q.ColorHex)} pageCap={q.PageCap}");

        List<WallhavenWallpaper> wallpapers;
        if (CollectionId != 0)
            wallpapers = await _client.GetCollectionAsync(CollectionId, ct);
        else
            wallpapers = await _client.SearchAsync(q, ct);

        if (wallpapers.Count == 0) { LogService.Warn("Fetch returned 0 wallpapers."); return null; }

        var all = wallpapers;
        wallpapers = wallpapers.Where(w => !BlocklistService.IsBlocked(w.Id)).ToList();
        if (wallpapers.Count == 0) { LogService.Warn("All fetched wallpapers are blocked."); return null; }

        wallpapers = wallpapers
            .Where(w => !HistoryService.IsApplied(w.Id) || HistoryService.IsFavorite(w.Id))
            .ToList();
        if (wallpapers.Count == 0)
        {
            LogService.Info("All candidates already applied — allowing repeats.");
            wallpapers = all;
        }
        LogService.Info($"Candidates after filters: {wallpapers.Count}");

        var monitors = MultiMonitor ? MultiMonitorSetter.GetMonitorIds() : new List<string>();
        int need = Math.Max(1, monitors.Count);
        var picks = wallpapers.OrderBy(_ => Random.Shared.Next()).Take(need).ToList();
        LogService.Info($"Picks: {string.Join(", ", picks.Select(p => p.Id))}");

        var paths = new List<string>();
        foreach (var p in picks)
            paths.Add(await _client.DownloadAsync(p, _cacheFolder, ct));

        if (monitors.Count > 1)
        {
            try { MultiMonitorSetter.SetWallpapers(monitors, paths, fit); }
            catch
            {
                LogService.Warn("Multi-monitor COM failed — falling back to single wallpaper.");
                WallpaperSetter.Set(paths[0]);
            }
        }
        else
        {
            WallpaperSetter.Set(paths[0]);
        }

        for (int i = 0; i < picks.Count; i++)
            HistoryService.Record(picks[i], paths[i], q.Tag);

        if (CachePruneEnabled)
            CacheService.Prune(_cacheFolder, CacheMaxFiles, CacheMaxBytes, HistoryService.FavoriteIds());

        LogService.Info($"Applied {picks[0].Id} OK.");
        return new RotationResult { Wallpaper = picks[0], FilePath = paths[0], ExtraCount = picks.Count - 1 };
    }

    /// <summary>Offline rotation: random cached wallpapers, no network access.</summary>
    public RotationResult? RotateFromCache(WallpaperFit fit)
    {
        var candidates = HistoryService.Entries
            .Where(e => File.Exists(e.FilePath) && !BlocklistService.IsBlocked(e.Id)).ToList();
        LogService.Info($"Offline rotation: {candidates.Count} cached candidate(s).");
        if (candidates.Count == 0) return null;

        var monitors = MultiMonitor ? MultiMonitorSetter.GetMonitorIds() : new List<string>();
        int need = Math.Max(1, monitors.Count);
        var picks = candidates.OrderBy(_ => Random.Shared.Next()).Take(need).ToList();
        var paths = picks.Select(p => p.FilePath).ToList();

        if (monitors.Count > 1)
        {
            try { MultiMonitorSetter.SetWallpapers(monitors, paths, fit); }
            catch { WallpaperSetter.Set(paths[0]); }
        }
        else
        {
            WallpaperSetter.Set(paths[0]);
        }

        foreach (var p in picks) HistoryService.MarkApplied(p);
        LogService.Info($"Applied (cache) {picks[0].Id} OK.");
        return new RotationResult
        {
            Wallpaper = new WallhavenWallpaper { Id = picks[0].Id, Resolution = picks[0].Resolution },
            FilePath = paths[0],
            ExtraCount = picks.Count - 1
        };
    }

    /// <summary>Favorite boost: set a random cached favorite, nothing else.</summary>
    public RotationResult? RotateFromFavorites(WallpaperFit fit)
    {
        var favs = HistoryService.Entries
            .Where(e => e.Favorite && File.Exists(e.FilePath) && !BlocklistService.IsBlocked(e.Id)).ToList();
        if (favs.Count == 0) { LogService.Info("Boost won the roll but no cached favorites — falling through to search."); return null; }

        var monitors = MultiMonitor ? MultiMonitorSetter.GetMonitorIds() : new List<string>();
        int need = Math.Max(1, monitors.Count);
        var picks = favs.OrderBy(_ => Random.Shared.Next()).Take(need).ToList();
        var paths = picks.Select(p => p.FilePath).ToList();

        if (monitors.Count > 1)
        {
            try { MultiMonitorSetter.SetWallpapers(monitors, paths, fit); }
            catch { WallpaperSetter.Set(paths[0]); }
        }
        else
        {
            WallpaperSetter.Set(paths[0]);
        }

        foreach (var p in picks) HistoryService.MarkApplied(p);
        LogService.Info($"Applied (favorite boost) {picks[0].Id} OK.");
        return new RotationResult
        {
            Wallpaper = new WallhavenWallpaper { Id = picks[0].Id, Resolution = picks[0].Resolution },
            FilePath = paths[0],
            ExtraCount = picks.Count - 1
        };
    }
}