namespace WallpaperRotator.Services;

public static class CacheService
{
    public const int DefaultMaxFiles = 200;
    public const long DefaultMaxBytes = 2L * 1024 * 1024 * 1024;   // 2 GB

    public static string Folder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WallpaperRotator");

    /// <summary>Deletes oldest wallpapers until within the given caps.</summary>
    public static void Prune(string folder, int maxFiles, long maxBytes, IEnumerable<string>? keepIds = null)
    {
        if (maxFiles <= 0 || maxBytes <= 0) return;
        var dir = new DirectoryInfo(folder);
        if (!dir.Exists) return;
        var keep = (keepIds ?? Enumerable.Empty<string>()).ToList();
        var all = dir.GetFiles("wallhaven-*").ToList();
        var deletable = all.Where(f => !keep.Any(id => f.Name.Contains(id)))
                           .OrderBy(f => f.LastWriteTimeUtc).ToList();
        long total = all.Sum(f => f.Length);
        int i = 0, deleted = 0;
        while (i < deletable.Count && (all.Count - deleted > maxFiles || total > maxBytes))
        {
            total -= deletable[i].Length;
            TryDelete(deletable[i]);
            i++;
            deleted++;
        }
        if (deleted > 0)
            LogService.Info($"Prune: deleted {deleted} file(s); {all.Count - deleted} remain " +
                            $"(caps: {maxFiles} files / {maxBytes / (1024 * 1024)} MB; {keep.Count} favorite(s) protected).");
    }

    public static void Clear(string folder, IEnumerable<string>? keepIds = null)
    {
        var dir = new DirectoryInfo(folder);
        if (!dir.Exists) return;
        var keep = (keepIds ?? Enumerable.Empty<string>()).ToList();
        int deleted = 0;
        foreach (var f in dir.GetFiles("wallhaven-*"))
        {
            if (keep.Any(id => f.Name.Contains(id))) continue;
            TryDelete(f);
            deleted++;
        }
        LogService.Info($"Clear: deleted {deleted} file(s); {keep.Count} favorite(s) kept.");
    }

    private static void TryDelete(FileInfo f)
    {
        try { f.Delete(); }
        catch { LogService.Warn($"Could not delete {f.Name} (in use?)."); }
    }
}