using System.Diagnostics;
using System.Runtime.InteropServices;
using BrowserSwitcher.Models;
using Microsoft.Win32;

namespace BrowserSwitcher.Services;

/// <summary>
/// 尝试将指定浏览器设为系统默认；失败时回退打开系统设置页。
/// </summary>
public sealed class DefaultBrowserSwitcher
{
    private readonly SettingsLauncher _settingsLauncher;
    private readonly DefaultBrowserResolver _defaultBrowserResolver;
    private readonly Func<string, string?> _findRegisteredAppName;
    private readonly Func<string, bool> _trySetAppAsDefault;

    /// <summary>
    /// 使用真实系统 API。
    /// </summary>
    public DefaultBrowserSwitcher(
        SettingsLauncher? settingsLauncher = null,
        DefaultBrowserResolver? defaultBrowserResolver = null)
        : this(
            settingsLauncher ?? new SettingsLauncher(),
            defaultBrowserResolver ?? new DefaultBrowserResolver(),
            FindRegisteredAppName,
            TrySetAppAsDefaultViaCom)
    {
    }

    /// <summary>
    /// 注入依赖，便于测试。
    /// </summary>
    public DefaultBrowserSwitcher(
        SettingsLauncher settingsLauncher,
        DefaultBrowserResolver defaultBrowserResolver,
        Func<string, string?> findRegisteredAppName,
        Func<string, bool> trySetAppAsDefault)
    {
        _settingsLauncher = settingsLauncher
            ?? throw new ArgumentNullException(nameof(settingsLauncher));
        _defaultBrowserResolver = defaultBrowserResolver
            ?? throw new ArgumentNullException(nameof(defaultBrowserResolver));
        _findRegisteredAppName = findRegisteredAppName
            ?? throw new ArgumentNullException(nameof(findRegisteredAppName));
        _trySetAppAsDefault = trySetAppAsDefault
            ?? throw new ArgumentNullException(nameof(trySetAppAsDefault));
    }

    /// <summary>
    /// 构建指向某已注册应用的默认应用设置深链。
    /// </summary>
    /// <param name="registeredName">RegisteredApplications 名称。</param>
    /// <param name="perUser">true 使用 registeredAppUser，否则 registeredAppMachine。</param>
    /// <returns>ms-settings URI。</returns>
    public static string BuildDeepLink(string registeredName, bool perUser)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registeredName);
        var key = perUser ? "registeredAppUser" : "registeredAppMachine";
        return "ms-settings:defaultapps?"
               + key
               + "="
               + Uri.EscapeDataString(registeredName.Trim());
    }

    /// <summary>
    /// 尝试切换默认浏览器。
    /// </summary>
    /// <param name="browser">目标浏览器。</param>
    /// <param name="knownBrowsers">当前列表，用于切换后校验。</param>
    /// <returns>切换结果。</returns>
    public BrowserSwitchResult Switch(
        BrowserInfo browser,
        IReadOnlyList<BrowserInfo> knownBrowsers)
    {
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(knownBrowsers);

        if (browser.IsCurrentDefault)
        {
            return BrowserSwitchResult.AlreadyDefault(browser.DisplayName);
        }

        if (browser.Status == BrowserStatus.NotFound
            || string.IsNullOrWhiteSpace(browser.ExecutablePath))
        {
            _settingsLauncher.OpenDefaultApps();
            return BrowserSwitchResult.FallbackToSettings(
                browser.DisplayName,
                "未找到该浏览器可执行文件，已打开系统设置。");
        }

        var registeredName = _findRegisteredAppName(browser.Id)
                             ?? GuessRegisteredName(browser);

        // 1) 尝试 COM 静默设为默认
        if (!string.IsNullOrWhiteSpace(registeredName)
            && _trySetAppAsDefault(registeredName))
        {
            var currentId = _defaultBrowserResolver.ResolveCurrentDefaultId(
                knownBrowsers);
            if (currentId == browser.Id)
            {
                return BrowserSwitchResult.Switched(browser.DisplayName);
            }
        }

        // 2) 深链到该应用的默认应用页（比总页更近一步）
        if (!string.IsNullOrWhiteSpace(registeredName)
            && TryOpenDeepLink(registeredName!))
        {
            return BrowserSwitchResult.FallbackToSettings(
                browser.DisplayName,
                $"无法静默切换，已打开「{browser.DisplayName}」的系统默认应用页，请点一次确认。");
        }

        // 3) 总页回退
        _settingsLauncher.OpenDefaultApps();
        return BrowserSwitchResult.FallbackToSettings(
            browser.DisplayName,
            $"无法静默切换，已打开系统默认应用页，请选择：{browser.DisplayName}");
    }

    private static string GuessRegisteredName(BrowserInfo browser)
    {
        return browser.Id switch
        {
            "chrome" => "Google Chrome",
            "edge" => "Microsoft Edge",
            "brave" => "Brave",
            "ie" => "Internet Explorer",
            _ => browser.DisplayName
        };
    }

    private static bool TryOpenDeepLink(string registeredName)
    {
        foreach (var perUser in new[] { false, true })
        {
            try
            {
                var uri = BuildDeepLink(registeredName, perUser);
                Process.Start(new ProcessStartInfo
                {
                    FileName = uri,
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception)
            {
                // 尝试另一种注册范围
            }
        }

        return false;
    }

    private static string? FindRegisteredAppName(string browserId)
    {
        var candidates = browserId switch
        {
            "chrome" => new[] { "Google Chrome", "Chrome" },
            "edge" => new[] { "Microsoft Edge", "MSEdge" },
            "brave" => new[] { "Brave", "BraveBrowser" },
            "ie" => new[] { "Internet Explorer" },
            _ => Array.Empty<string>()
        };

        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            try
            {
                using var key = hive.OpenSubKey(@"Software\RegisteredApplications");
                if (key == null)
                {
                    continue;
                }

                foreach (var name in key.GetValueNames())
                {
                    foreach (var candidate in candidates)
                    {
                        if (string.Equals(
                                name,
                                candidate,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return name;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 继续下一 hive
            }
        }

        return candidates.FirstOrDefault();
    }

    private static bool TrySetAppAsDefaultViaCom(string appRegistryName)
    {
        try
        {
            var type = Type.GetTypeFromCLSID(
                new Guid("591209c7-767b-42b2-9fba-44ee4615f2c7"));
            if (type == null)
            {
                return false;
            }

            var com = Activator.CreateInstance(type);
            if (com is not IApplicationAssociationRegistration reg)
            {
                return false;
            }

            reg.SetAppAsDefault(
                appRegistryName,
                "http",
                AssociationType.UrlProtocol);
            reg.SetAppAsDefault(
                appRegistryName,
                "https",
                AssociationType.UrlProtocol);
            reg.SetAppAsDefault(
                appRegistryName,
                ".html",
                AssociationType.FileExtension);
            reg.SetAppAsDefault(
                appRegistryName,
                ".htm",
                AssociationType.FileExtension);
            return true;
        }
        catch (COMException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (InvalidCastException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private enum AssociationType
    {
        FileExtension = 0,
        UrlProtocol = 1
    }

    private enum AssociationLevel
    {
        Machine = 0,
        Effective = 1,
        User = 2
    }

    [ComImport]
    [Guid("4e530b0a-e611-4c77-a3ac-9031d022281b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationAssociationRegistration
    {
        void QueryCurrentDefault(
            [MarshalAs(UnmanagedType.LPWStr)] string query,
            AssociationType associationType,
            AssociationLevel associationLevel,
            [MarshalAs(UnmanagedType.LPWStr)] out string association);

        void QueryAppIsDefault(
            [MarshalAs(UnmanagedType.LPWStr)] string query,
            AssociationType associationType,
            AssociationLevel associationLevel,
            [MarshalAs(UnmanagedType.LPWStr)] string appRegistryName,
            out bool isDefault);

        void QueryAppIsDefaultAll(
            AssociationLevel associationLevel,
            [MarshalAs(UnmanagedType.LPWStr)] string appRegistryName,
            out bool isDefault);

        void SetAppAsDefault(
            [MarshalAs(UnmanagedType.LPWStr)] string appRegistryName,
            [MarshalAs(UnmanagedType.LPWStr)] string association,
            AssociationType associationType);

        void SetAppAsDefaultAll(
            [MarshalAs(UnmanagedType.LPWStr)] string appRegistryName);

        void ClearUserAssociations();
    }
}

/// <summary>
/// 默认浏览器切换结果。
/// </summary>
public sealed class BrowserSwitchResult
{
    private BrowserSwitchResult(
        BrowserSwitchOutcome outcome,
        string browserName,
        string message)
    {
        Outcome = outcome;
        BrowserName = browserName;
        Message = message;
    }

    /// <summary>结果类型。</summary>
    public BrowserSwitchOutcome Outcome { get; }

    /// <summary>目标浏览器显示名。</summary>
    public string BrowserName { get; }

    /// <summary>面向用户的说明。</summary>
    public string Message { get; }

    /// <summary>是否已成功静默切换。</summary>
    public bool IsSilentSuccess => Outcome == BrowserSwitchOutcome.Switched;

    public static BrowserSwitchResult AlreadyDefault(string name) =>
        new(
            BrowserSwitchOutcome.AlreadyDefault,
            name,
            $"{name} 已是当前默认浏览器。");

    public static BrowserSwitchResult Switched(string name) =>
        new(
            BrowserSwitchOutcome.Switched,
            name,
            $"已切换默认浏览器为：{name}");

    public static BrowserSwitchResult FallbackToSettings(
        string name,
        string message) =>
        new(BrowserSwitchOutcome.FallbackToSettings, name, message);
}

/// <summary>切换结果枚举。</summary>
public enum BrowserSwitchOutcome
{
    AlreadyDefault,
    Switched,
    FallbackToSettings
}
