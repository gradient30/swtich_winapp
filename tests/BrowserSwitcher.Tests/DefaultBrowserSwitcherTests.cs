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
    public void BuildDeepLink_UsesRegisteredAppUser()
    {
        var uri = DefaultBrowserSwitcher.BuildDeepLink("Brave", true);
        Assert.Equal(
            "ms-settings:defaultapps?registeredAppUser=Brave",
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

    [Fact]
    public void Switch_ExternalToolSuccess_ReturnsSwitched()
    {
        var browsers = new List<BrowserInfo>
        {
            new()
            {
                Id = "edge",
                DisplayName = "Microsoft Edge",
                ExecutablePath = @"C:\msedge.exe",
                Status = BrowserStatus.Installed
            },
            new()
            {
                Id = "chrome",
                DisplayName = "Google Chrome",
                ExecutablePath = @"C:\chrome.exe",
                Status = BrowserStatus.Installed,
                IsCurrentDefault = true
            }
        };

        var resolver = new DefaultBrowserResolver(() => "MSEdgeHTM", _ => null);
        var switcher = new DefaultBrowserSwitcher(
            new SettingsLauncher(),
            resolver,
            _ => "Microsoft Edge",
            _ => false,
            _ => true,
            _ => false);

        var result = switcher.Switch(browsers[0], browsers);

        Assert.Equal(BrowserSwitchOutcome.Switched, result.Outcome);
        Assert.True(result.IsSilentSuccess);
    }

    [Fact]
    public void Switch_AssociationUi_ReturnsFallback()
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

        // 解析器保持「仍是 Edge」，模拟直切失败
        var resolver = new DefaultBrowserResolver(() => "MSEdgeHTM", _ => null);
        var switcher = new DefaultBrowserSwitcher(
            new SettingsLauncher(),
            resolver,
            _ => "Google Chrome",
            _ => false,
            _ => false,
            _ => true);

        var result = switcher.Switch(browsers[0], browsers);

        Assert.Equal(BrowserSwitchOutcome.FallbackToSettings, result.Outcome);
        Assert.Contains("关联页", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSetDefaultBrowserArgument_MapsKnownIds()
    {
        Assert.Equal(
            "chrome",
            DefaultBrowserSwitcher.BuildSetDefaultBrowserArgument("chrome"));
        Assert.Equal(
            "edge",
            DefaultBrowserSwitcher.BuildSetDefaultBrowserArgument("edge"));
        Assert.Equal(
            "ie",
            DefaultBrowserSwitcher.BuildSetDefaultBrowserArgument("ie"));
        Assert.Equal(
            "HKLM Brave",
            DefaultBrowserSwitcher.BuildSetDefaultBrowserArgument("brave"));
        Assert.Null(
            DefaultBrowserSwitcher.BuildSetDefaultBrowserArgument("firefox"));
    }
}
