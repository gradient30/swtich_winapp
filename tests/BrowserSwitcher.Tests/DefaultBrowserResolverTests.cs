using BrowserSwitcher.Models;
using BrowserSwitcher.Services;

namespace BrowserSwitcher.Tests;

public class DefaultBrowserResolverTests
{
    [Fact]
    public void Resolve_ChromeProgId_ReturnsChromeId()
    {
        var browsers = CreateSampleBrowsers();
        var resolver = new DefaultBrowserResolver(
            () => "ChromeHTML",
            _ => null);

        var id = resolver.ResolveCurrentDefaultId(browsers);

        Assert.Equal("chrome", id);
    }

    [Fact]
    public void Resolve_EdgeCommandPath_MatchesEdge()
    {
        var browsers = CreateSampleBrowsers();
        var resolver = new DefaultBrowserResolver(
            () => "Some.Unknown.ProgId",
            _ => @"""C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"" --single-argument %1");

        var id = resolver.ResolveCurrentDefaultId(browsers);

        Assert.Equal("edge", id);
    }

    [Fact]
    public void ExtractExecutablePath_FromQuotedCommand()
    {
        var path = DefaultBrowserResolver.ExtractExecutablePath(
            @"""C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe"" %1");

        Assert.Equal(
            @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe",
            path);
    }

    [Fact]
    public void Resolve_UnknownProgIdWithoutPath_ReturnsNull()
    {
        var browsers = CreateSampleBrowsers();
        var resolver = new DefaultBrowserResolver(
            () => "FirefoxURL-abc",
            _ => null);

        Assert.Null(resolver.ResolveCurrentDefaultId(browsers));
    }

    private static List<BrowserInfo> CreateSampleBrowsers()
    {
        return new List<BrowserInfo>
        {
            new()
            {
                Id = "brave",
                DisplayName = "Brave",
                ExecutablePath =
                    @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe",
                Status = BrowserStatus.Installed
            },
            new()
            {
                Id = "chrome",
                DisplayName = "Google Chrome",
                ExecutablePath =
                    @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                Status = BrowserStatus.Installed
            },
            new()
            {
                Id = "ie",
                DisplayName = "Internet Explorer",
                ExecutablePath = null,
                Status = BrowserStatus.NotFound
            },
            new()
            {
                Id = "edge",
                DisplayName = "Microsoft Edge",
                ExecutablePath =
                    @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                Status = BrowserStatus.Installed
            }
        };
    }
}
