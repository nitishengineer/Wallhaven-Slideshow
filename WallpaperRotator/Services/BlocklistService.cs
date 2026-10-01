using System.Text.Json;

namespace WallpaperRotator.Services;

public class BlockedEntry
{
    public string Id { get; set; } = "";
    public string Resolution { get; set; } = "";
    public DateTime BlockedAt { get; set; }
}

/// <summary>Permanent "never show again" list. Beats favorites, history and cache modes.</summary>
public static class BlocklistService
{
    private static readonly string BlocklistPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WallpaperRotator", "blocklist.json");

    public static List<BlockedEntry> Entries { get; } = Load();

    /// <summary>Fires after every Add/Remove so any UI bound to the list can refresh.</summary>
    public static event Action? Changed;

    private static List<BlockedEntry> Load()
    {
        try
        {
            if (File.Exists(BlocklistPath))
                return JsonSerializer.Deserialize<List<BlockedEntry>>(File.ReadAllText(BlocklistPath)) ?? new();
        }
        catch { }
        return new();
    }

    public static void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(BlocklistPath)!);
        File.WriteAllText(BlocklistPath,
            JsonSerializer.Serialize(Entries, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static bool IsBlocked(string id) => Entries.Any(e => e.Id == id);

    public static void Add(string id, string resolution = "")
    {
        if (string.IsNullOrEmpty(id) || IsBlocked(id)) return;
        Entries.Insert(0, new BlockedEntry { Id = id, Resolution = resolution, BlockedAt = DateTime.Now });
        Save();
        LogService.Info($"Blocked {id}.");
        Changed?.Invoke();
    }

    public static void Remove(string id)
    {
        if (Entries.RemoveAll(e => e.Id == id) > 0)
        {
            Save();
            LogService.Info($"Unblocked {id}.");
            Changed?.Invoke();
        }
    }
}