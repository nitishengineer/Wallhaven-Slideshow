namespace WallpaperRotator.Services;

/// <summary>Tiny rotating diagnostics log with a live in-process feed.</summary>
public static class LogService
{
    private static readonly object Gate = new();
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WallpaperRotator", "rotator.log");
    private const long MaxBytes = 1_000_000;   // rotate at ~1 MB, keep one .old backup

    public static string LogFilePath => LogPath;

    /// <summary>Raised with every formatted line so UIs can tail live (no polling).</summary>
    public static event Action<string>? LineWritten;

    public static void Info(string message) => Write("INFO ", message);
    public static void Warn(string message) => Write("WARN ", message);
    public static void Error(string message) => Write("ERROR", message);

    /// <summary>Resets the log file; the reset itself is logged.</summary>
    public static void Clear()
    {
        lock (Gate)
        {
            try { File.WriteAllText(LogPath, ""); } catch { }
        }
        Info("Log cleared by user.");
    }

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                var fi = new FileInfo(LogPath);
                if (fi.Exists && fi.Length > MaxBytes)
                {
                    var old = LogPath + ".old";
                    if (File.Exists(old)) File.Delete(old);
                    fi.MoveTo(old);
                }
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
        }
        catch { /* logging must never break the app */ }
        try { LineWritten?.Invoke(line); } catch { }
    }
}