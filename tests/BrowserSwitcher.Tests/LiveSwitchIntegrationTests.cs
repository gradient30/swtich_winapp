using BrowserSwitcher.Services;
using Microsoft.Win32;

namespace BrowserSwitcher.Tests;

/// <summary>
/// 本机实机验证 UserChoice 直写（Chrome ↔ Brave）。
/// </summary>
public class LiveSwitchIntegrationTests
{
    private static string? ReadHttpProgId()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice");
        return key?.GetValue("ProgId") as string;
    }

    private static bool HttpAndHttpsMatch(string expectedProgId)
    {
        foreach (var assoc in new[] { "http", "https" })
        {
            var path =
                $@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\{assoc}\UserChoice";
            using var key = Registry.CurrentUser.OpenSubKey(path);
            var progId = key?.GetValue("ProgId") as string;
            if (!string.Equals(progId, expectedProgId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    [Fact]
    public void LiveSwitch_ChromeAndBrave_RoundTrip()
    {
        if (Environment.GetEnvironmentVariable("RUN_LIVE_SWITCH_TESTS") != "1")
        {
            return;
        }

        var before = ReadHttpProgId();
        Assert.NotNull(before);

        var targetId = before.Contains("Brave", StringComparison.OrdinalIgnoreCase)
            ? "chrome"
            : "brave";
        var expectedProgId = targetId == "chrome" ? "ChromeHTML" : "BraveHTML";

        var switched = UserChoiceAssociationWriter.TrySetDefaultBrowserById(targetId);
        Assert.True(switched, "TrySetDefaultBrowserById returned false (UCPD or ACL?)");
        Assert.True(
            HttpAndHttpsMatch(expectedProgId),
            $"http/https not all {expectedProgId} after switch");

        var backId = targetId == "chrome" ? "brave" : "chrome";
        var backProgId = backId == "chrome" ? "ChromeHTML" : "BraveHTML";
        Assert.True(UserChoiceAssociationWriter.TrySetDefaultBrowserById(backId));
        Assert.True(HttpAndHttpsMatch(backProgId));

        // 连续往返，模拟多次托盘切换
        for (var i = 0; i < 3; i++)
        {
            Assert.True(UserChoiceAssociationWriter.TrySetDefaultBrowserById("chrome"));
            Thread.Sleep(300);
            Assert.True(HttpAndHttpsMatch("ChromeHTML"));

            Assert.True(UserChoiceAssociationWriter.TrySetDefaultBrowserById("brave"));
            Thread.Sleep(300);
            Assert.True(HttpAndHttpsMatch("BraveHTML"));
        }
    }
}
