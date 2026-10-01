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