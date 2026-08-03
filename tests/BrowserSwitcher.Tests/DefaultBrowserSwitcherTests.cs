using BrowserSwitcher.Models;
using BrowserSwitcher.Services;

namespace BrowserSwitcher.Tests;

public class DefaultBrowserSwitcherTests
{
    [Fact]
    public void BuildDeepLink_UsesRegisteredAppMachine()
    {
        var uri = DefaultBrowserSwitcher.BuildDeepLink("Google Chrome", false);
        Assert.Equal(
            "ms-settings:defaultapps?registeredAppMachine=Google%20Chrome",
            uri);
    }

    [Fact]
    public void Switch_AlreadyDefault_DoesNotCallSetters()
    {
        var setCalled = false;
        var launcher = new SettingsLauncher();
        var resolver = new DefaultBrowserResolver(() => "ChromeHTML", _ => null);
        var switcher = new DefaultBrowserSwitcher(
            launcher,
            resolver,
            _ => "Google Chrome",
            _ =>
            {
                setCalled = true;
                return true;
            });

        var browser = new BrowserInfo
        {
            Id = "chrome",
            DisplayName = "Google Chrome",
            ExecutablePath = @"C:\chrome.exe",
            Status = BrowserStatus.Installed,
            IsCurrentDefault = true
        };

        var result = switcher.Switch(browser, new[] { browser });

        Assert.Equal(BrowserSwitchOutcome.AlreadyDefault, result.Outcome);
        Assert.False(setCalled);
    }

    [Fact]
    public void Switch_SilentSuccess_ReturnsSwitched()
    {
        var browsers = new List<BrowserInfo>
        {
            new()
            {
                Id = "chrome",
                DisplayName = "Google Chrome",
                ExecutablePath = @"C:\chrome.exe",
                Status = BrowserStatus.Installed
            },
            new()
            {
                Id = "edge",
                DisplayName = "Microsoft Edge",
                ExecutablePath = @"C:\msedge.exe",
                Status = BrowserStatus.Installed,
                IsCurrentDefault = true
            }
        };

        var resolver = new DefaultBrowserResolver(() => "ChromeHTML", _ => null);
        var switcher = new DefaultBrowserSwitcher(
            new SettingsLauncher(),
            resolver,
            _ => "Google Chrome",
            _ => true);

        var result = switcher.Switch(browsers[0], browsers);

        Assert.Equal(BrowserSwitchOutcome.Switched, result.Outcome);
        Assert.True(result.IsSilentSuccess);
    }
}
