using System.Text.Json;

namespace WallpaperRotator.Services;

public class AppSettings
{
    public string Tag { get; set; } = "abstract";
    public int IntervalMinutes { get; set; } = 60;
    public string Sorting { get; set; } = "random";
    public string Fit { get; set; } = "Fill";
    public bool CachePruneEnabled { get; set; } = true;
    public int CacheMaxFiles { get; set; } = 200;
    public double CacheMaxMb { get; set; } = 2048;
    public bool ScheduleEnabled { get; set; }
    public string MorningTag { get; set; } = "nature";
    public string AfternoonTag { get; set; } = "minimalist";
    public string EveningTag { get; set; } = "cyberpunk";
    public string NightTag { get; set; } = "space";
    public bool ResMatchScreen { get; set; } = true;
    public int MinWidth { get; set; } = 1920;
    public int MinHeight { get; set; } = 1080;
    public bool PauseOnFullscreen { get; set; } = true;
    public bool DarkMode { get; set; }
    public bool OfflineMode { get; set; }
    public bool MultiMonitor { get; set; }
    public string ApiKey { get; set; } = "";
    public int CollectionId { get; set; }
    public bool CatGeneral { get; set; } = true;
    public bool CatAnime { get; set; } = true;
    public bool CatPeople { get; set; }
    public bool PuritySketchy { get; set; }
    public string ResMode { get; set; } = "atleast";
    public string Ratio { get; set; } = "";
    public string ColorHex { get; set; } = "";
    public int PageCap { get; set; } = 10;
    public bool HistoryCapEnabled { get; set; } = true;
    public int HistoryCap { get; set; } = 500;
    public bool FavoriteBoostEnabled { get; set; }
    public int FavoriteBoostPercent { get; set; } = 30;
    public bool SolarScheduleEnabled { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool UpdateChecksEnabled { get; set; } = true;
    public DateTime LastUpdateCheckUtc { get; set; }
    public string SkippedVersion { get; set; } = "";

    // ----- Tag pool (Tags tab) -----
    /// <summary>When on, rotation picks one tag from <see cref="PoolTags"/> per change instead of the single Category.</summary>
    public bool UseTagPool { get; set; }
    /// <summary>Ordered list of tags to rotate through (one included tag per wallpaper fetch).</summary>
    public List<string> PoolTags { get; set; } = new();
    /// <summary>Tags whose wallpapers are always filtered out, whether the pool is on or off.</summary>
    public List<string> ExcludedTags { get; set; } = new();
    /// <summary>"order" (follow the Included ranking) or "shuffle" (shuffle bag).</summary>
    public string TagOrderMode { get; set; } = "order";
    /// <summary>Next index used in ordered mode.</summary>
    public int PoolCursor { get; set; }
    /// <summary>Tags left in the current shuffle bag (shuffle mode only).</summary>
    public List<string> PoolBag { get; set; } = new();
    /// <summary>Last tag the shuffle bag handed out, so a bag refill never repeats it immediately.</summary>
    public string PoolLast { get; set; } = "";
}

public static class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WallpaperRotator", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new();
        }
        catch { }
        return new();
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath,
            JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }
}