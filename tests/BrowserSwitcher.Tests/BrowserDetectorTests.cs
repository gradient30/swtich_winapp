using BrowserSwitcher.Models;
using BrowserSwitcher.Services;

namespace BrowserSwitcher.Tests;

public class BrowserDetectorTests
{
    [Fact]
    public void Detect_ManualPathExists_OverridesAsManual()
    {
        var tempExe = Path.Combine(Path.GetTempPath(), $"chrome-{Guid.NewGuid()}.exe");
        File.WriteAllBytes(tempExe, Array.Empty<byte>());
        try
        {
            var config = new AppConfig
            {
                ManualPaths = { ["Google Chrome"] = tempExe }
            };
            var detector = new BrowserDetector();
            var list = detector.Detect(config);
            var chrome = Assert.Single(list, b => b.Id == "chrome");
            Assert.Equal(BrowserStatus.Manual, chrome.Status);
            Assert.Equal(tempExe, chrome.ExecutablePath);
        }
        finally
        {
            File.Delete(tempExe);
        }
    }

    [Fact]
    public void Detect_AlwaysReturnsFourKnownBrowsers()
    {
        var detector = new BrowserDetector();
        var list = detector.Detect(new AppConfig());
        Assert.Equal(4, list.Count);
        Assert.Contains(list, b => b.Id == "brave");
        Assert.Contains(list, b => b.Id == "chrome");
        Assert.Contains(list, b => b.Id == "ie");
        Assert.Contains(list, b => b.Id == "edge");
    }
}
