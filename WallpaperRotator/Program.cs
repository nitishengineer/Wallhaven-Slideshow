using System.Threading;
using WallpaperRotator.Services;

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

        // Tell WinForms which colour mode the app starts in, so native pieces we do not
        // paint ourselves (scrollbars, native menus) pick the matching look. This has to
        // happen before the first window is created and cannot be changed afterwards.
        var settings = SettingsService.Load();
        Theme.Init(settings.DarkMode);
#pragma warning disable WFO5001 // Application.SetColorMode is still marked experimental in WinForms.
        Application.SetColorMode(settings.DarkMode ? SystemColorMode.Dark : SystemColorMode.Classic);
#pragma warning restore WFO5001

        Application.Run(new Form1());
    }
}
