using System.Runtime.InteropServices;

namespace WallpaperRotator.Services;

[ComImport, Guid("C2CF3110-44C9-4D52-9B44-0D1B4B0E3408"), ClassInterface(ClassInterfaceType.None)]
internal class DesktopWallpaperClass { }

[StructLayout(LayoutKind.Sequential)]
internal struct MonitorRect { public int Left, Top, Right, Bottom; }

internal enum DesktopWallpaperPosition { Center = 0, Tile = 1, Stretch = 2, Fit = 3, Fill = 4, Span = 5 }

// Method order MUST match the COM vtable exactly (SetWallpaper, GetWallpaper,
// GetMonitorDevicePathAt, GetMonitorDevicePathCount, GetMonitorRECT,
// SetBackgroundColor, GetBackgroundColor, SetPosition)
[ComImport, Guid("B92B56A9-8288-4618-A08C-3E4FF0D3CE36"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDesktopWallpaper
{
    void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorId,
                      [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);

    void GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorId,
                      [Out, MarshalAs(UnmanagedType.LPWStr)] out string wallpaper);

    void GetMonitorDevicePathAt(uint monitorIndex,
                                [Out, MarshalAs(UnmanagedType.LPWStr)] out string monitorId);

    uint GetMonitorDevicePathCount();

    void GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorId, out MonitorRect rect);

    void SetBackgroundColor(uint color);

    uint GetBackgroundColor();

    void SetPosition(DesktopWallpaperPosition position);
}

public static class MultiMonitorSetter
{
    /// <summary>Device paths of all connected monitors (empty if COM unavailable).</summary>
    public static List<string> GetMonitorIds()
    {
        var ids = new List<string>();
        try
        {
            var dw = (IDesktopWallpaper)new DesktopWallpaperClass();
            uint count = dw.GetMonitorDevicePathCount();
            for (uint i = 0; i < count; i++)
            {
                dw.GetMonitorDevicePathAt(i, out var id);
                ids.Add(id);
            }
        }
        catch { /* COM unavailable -> single-monitor fallback */ }
        return ids;
    }

    /// <summary>Sets a different wallpaper on each monitor.</summary>
    public static void SetWallpapers(IReadOnlyList<string> monitorIds, IReadOnlyList<string> paths, WallpaperFit fit)
    {
        var dw = (IDesktopWallpaper)new DesktopWallpaperClass();

        dw.SetPosition(fit switch
        {
            WallpaperFit.Fit => DesktopWallpaperPosition.Fit,
            WallpaperFit.Stretch => DesktopWallpaperPosition.Stretch,
            WallpaperFit.Center => DesktopWallpaperPosition.Center,
            WallpaperFit.Tile => DesktopWallpaperPosition.Tile,
            WallpaperFit.Span => DesktopWallpaperPosition.Span,
            _ => DesktopWallpaperPosition.Fill
        });

        for (int i = 0; i < monitorIds.Count && i < paths.Count; i++)
            dw.SetWallpaper(monitorIds[i], paths[i]);
    }
}