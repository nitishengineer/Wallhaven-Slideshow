using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace WallpaperRotator.Services;

public enum WallpaperFit { Fill, Fit, Stretch, Center, Tile, Span }

public static class WallpaperSetter
{
    private const int SPI_SETDESKWALLPAPER = 0x0014;
    private const int SPIF_UPDATEINIFILE = 0x01;
    private const int SPIF_SENDWININICHANGE = 0x02;

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int SystemParametersInfo(int uiAction, int uiParam, string pvParam, int fWinIni);

    public static void Set(string imagePath)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Wallpaper file not found.", imagePath);

        var result = SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, imagePath,
            SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);

        if (result == 0)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    /// <summary>Sets Windows' wallpaper fit mode via registry (applies on next Set call).</summary>
    public static void SetFit(WallpaperFit fit)
    {
        (string style, string tile) = fit switch
        {
            WallpaperFit.Fill => ("10", "0"),
            WallpaperFit.Fit => ("6", "0"),
            WallpaperFit.Stretch => ("2", "0"),
            WallpaperFit.Center => ("0", "0"),
            WallpaperFit.Tile => ("0", "1"),
            WallpaperFit.Span => ("22", "0"),
            _ => ("10", "0")
        };

        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
        if (key is null) return;
        key.SetValue("WallpaperStyle", style);
        key.SetValue("TileWallpaper", tile);
    }
}