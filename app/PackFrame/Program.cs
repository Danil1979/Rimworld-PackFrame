using Avalonia;
using System;

namespace PackFrame;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (AppUpdateService.TryRunHelper(args))
        {
            return;
        }

        if (SteamWorkshopClient.TryRunHelper(args))
        {
            return;
        }

        SteamWorkshopClient.RemoveLegacyAppIdFile();
        AppLog.Cleanup();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
