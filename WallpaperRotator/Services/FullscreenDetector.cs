using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace WallpaperRotator.Services;

public static class FullscreenDetector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int maxCount);

    /// <summary>True when the foreground window covers the entire primary screen (game, video, F11 browser…).</summary>
    public static bool IsFullscreenApp()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;

        var sb = new StringBuilder(256);
        GetClassName(hwnd, sb, sb.Capacity);
        var cls = sb.ToString();
        if (cls is "Progman" or "WorkerW") return false;    // that's just the desktop

        if (!GetWindowRect(hwnd, out var r)) return false;
        var screen = Screen.PrimaryScreen?.Bounds ?? System.Drawing.Rectangle.Empty;

        return r.Left <= screen.Left && r.Top <= screen.Top &&
               r.Right >= screen.Right && r.Bottom >= screen.Bottom;
    }
}