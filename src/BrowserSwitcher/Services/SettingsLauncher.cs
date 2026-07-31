using System.Diagnostics;

namespace BrowserSwitcher.Services;

public sealed class SettingsLauncher
{
    public static ProcessStartInfo CreateStartInfo()
    {
        return new ProcessStartInfo
        {
            FileName = "ms-settings:defaultapps",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Normal
        };
    }

    public void OpenDefaultApps()
    {
        Process.Start(CreateStartInfo());
    }
}
