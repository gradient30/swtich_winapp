using BrowserSwitcher.Models;
using BrowserSwitcher.Services;

namespace BrowserSwitcher.Tests;

public class ConfigStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsManualPaths()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bs-test-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            var store = new ConfigStore(dir);
            var config = new AppConfig
            {
                ManualPaths = new Dictionary<string, string>
                {
                    ["Google Chrome"] = @"C:\Program Files\Google\Chrome\Application\chrome.exe"
                }
            };
            store.Save(config);
            var loaded = store.Load();
            Assert.Equal(
                config.ManualPaths["Google Chrome"],
                loaded.ManualPaths["Google Chrome"]);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaultConfig()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bs-test-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "config.json"), "{not-json");
            var store = new ConfigStore(dir);
            var loaded = store.Load();
            Assert.NotNull(loaded.ManualPaths);
            Assert.Empty(loaded.ManualPaths);
            Assert.True(store.LastLoadUsedFallback);
            Assert.False(string.IsNullOrWhiteSpace(store.LastLoadFallbackReason));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Load_NullManualPaths_ReturnsEmptyDictionary()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bs-test-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(
                Path.Combine(dir, "config.json"),
                "{\"ManualPaths\":null}");
            var store = new ConfigStore(dir);
            var loaded = store.Load();
            Assert.NotNull(loaded.ManualPaths);
            Assert.Empty(loaded.ManualPaths);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
