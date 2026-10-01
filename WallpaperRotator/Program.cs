using System.Threading;

namespace WallpaperRotator;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, "WallpaperRotator.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Wallpaper Rotator is already running in the system tray.",
                "Wallpaper Rotator", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new Form1());
    }
}