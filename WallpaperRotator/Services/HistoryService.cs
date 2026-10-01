using System.Text.Json;
using WallpaperRotator.Models;

namespace WallpaperRotator.Services;

public class HistoryEntry
{
    public string Id { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string Resolution { get; set; } = "";
    public string Tag { get; set; } = "";
    public DateTime AppliedAt { get; set; }
    public bool Favorite { get; set; }
}

public static class HistoryService
{
    private static readonly string HistoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WallpaperRotator", "history.json");

    public static List<HistoryEntry> Entries { get; } = Load();

    /// <summary>How many non-favorite entries to keep. null = keep everything (never repeat).</summary>
    public static int? Cap { get; set; } = 500;

    private static List<HistoryEntry> Load()
    {
        try
        {
            if (File.Exists(HistoryPath))
                return JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(HistoryPath)) ?? new();
        }
        catch { }
        return new();
    }

    public static void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(HistoryPath)!);
        File.WriteAllText(HistoryPath,
            JsonSerializer.Serialize(Entries, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void Record(WallhavenWallpaper w, string filePath, string tag)
    {
        var existing = Entries.FirstOrDefault(e => e.Id == w.Id);
        if (existing is null)
        {
            Entries.Insert(0, new HistoryEntry
            {
                Id = w.Id,
                FilePath = filePath,
                Resolution = w.Resolution,
                Tag = tag,
                AppliedAt = DateTime.Now
            });
        }
        else
        {
            existing.FilePath = filePath;
            existing.Tag = tag;
            existing.AppliedAt = DateTime.Now;
            Entries.Remove(existing);
            Entries.Insert(0, existing);
        }

        TrimToCap();
        Save();
    }

    /// <summary>Removes the oldest non-favorite entries beyond Cap. Favorites are never trimmed.</summary>
    public static void TrimToCap()
    {
        int before = Entries.Count;
        if (Cap is not int cap) return;
        while (Entries.Count > cap)
        {
            int idx = -1;
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                if (!Entries[i].Favorite) { idx = i; break; }
            }
            if (idx < 0) break;      // only favorites left — stop
            Entries.RemoveAt(idx);
        }
        if (Entries.Count < before) LogService.Warn($"History trim: removed {before - Entries.Count} oldest non-favorite entr(ies) (cap {cap}).");
    }

    /// <summary>Moves an existing entry to the top with a fresh timestamp (re-apply).</summary>
    public static void MarkApplied(HistoryEntry e)
    {
        e.AppliedAt = DateTime.Now;
        Entries.Remove(e);
        Entries.Insert(0, e);
        Save();
    }

    public static bool IsApplied(string id) => Entries.Any(e => e.Id == id);
    public static bool IsFavorite(string id) => Entries.Any(e => e.Id == id && e.Favorite);
    public static HashSet<string> FavoriteIds() =>
        Entries.Where(e => e.Favorite).Select(e => e.Id).ToHashSet();
}