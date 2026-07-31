using System.Diagnostics;
using BrowserSwitcher.Services;

namespace BrowserSwitcher.Tests;

public class SettingsLauncherTests
{
    [Fact]
    public void CreateStartInfo_UsesDefaultAppsUri()
    {
        var startInfo = SettingsLauncher.CreateStartInfo();

        Assert.Equal("ms-settings:defaultapps", startInfo.FileName);
        Assert.True(startInfo.UseShellExecute);
        Assert.Equal(ProcessWindowStyle.Normal, startInfo.WindowStyle);
    }
}
