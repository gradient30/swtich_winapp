using BrowserSwitcher.Services;
using Microsoft.Win32;

namespace BrowserSwitcher.Tests;

/// <summary>
/// 开机启动注册表写入测试（使用临时 HKCU 子键，避免污染真实 Run）。
/// </summary>
public class StartupRegistrationTests
{
    [Fact]
    public void Apply_EnableThenDisable_RoundTripsRegistration()
    {
        var hivePath = @"Software\BrowserSwitcherTests\" + Guid.NewGuid();
        var valueName = "BrowserSwitcher_Test";
        try
        {
            RegistryKey OpenKey(bool writable)
            {
                if (writable)
                {
                    return Registry.CurrentUser.CreateSubKey(hivePath)
                        ?? throw new InvalidOperationException(
                            "无法创建测试注册表键。");
                }

                return Registry.CurrentUser.OpenSubKey(hivePath, false)
                    ?? Registry.CurrentUser.CreateSubKey(hivePath)
                    ?? throw new InvalidOperationException(
                        "无法打开测试注册表键。");
            }

            var registration = new StartupRegistration(OpenKey, valueName);
            Assert.False(registration.IsRegistered());

            var exePath = @"C:\Tools\BrowserSwitcher\BrowserSwitcher.exe";
            registration.Apply(true, exePath);
            Assert.True(registration.IsRegistered());

            using (var key = OpenKey(false))
            {
                var stored = Assert.IsType<string>(
                    key.GetValue(valueName));
                Assert.Contains(
                    "BrowserSwitcher.exe",
                    stored,
                    StringComparison.OrdinalIgnoreCase);
            }

            registration.Apply(false, exePath);
            Assert.False(registration.IsRegistered());
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(hivePath, false);
        }
    }

    [Fact]
    public void Apply_EnableWithEmptyPath_Throws()
    {
        var registration = new StartupRegistration(
            _ => null,
            "BrowserSwitcher_Test");
        Assert.Throws<ArgumentException>(
            () => registration.Apply(true, "   "));
    }
}
