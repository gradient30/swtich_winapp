using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using BrowserSwitcher.Models;
using Microsoft.Win32;

namespace BrowserSwitcher.Services;

/// <summary>
/// 按官方与社区可行路径切换默认浏览器。
/// 说明：Windows 8+ 起微软要求经系统 UI 确认；2024 起 UCPD.sys
/// 会拦截对 UserChoice 的第三方写入。因此本实现优先：
/// 1) 可选社区工具 SetDefaultBrowser（若本机已安装）
/// 2) 旧版 COM SetAppAsDefault（多数新系统会失败）
/// 3) LaunchAdvancedAssociationUI / ms-settings 深链（官方推荐，一页一点确认）
/// </summary>
public sealed class DefaultBrowserSwitcher
{
    private readonly SettingsLauncher _settingsLauncher;
    private readonly DefaultBrowserResolver _defaultBrowserResolver;
    private readonly Func<string, string?> _findRegisteredAppName;
    private readonly Func<string, bool> _trySetAppAsDefault;
    private readonly Func<string, bool> _tryExternalSetDefaultBrowser;
    private readonly Func<string, bool> _tryLaunchAssociationUi;

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
            TrySetAppAsDefaultViaCom,
            TryExternalSetDefaultBrowser,
            TryLaunchAdvancedAssociationUi)
    {
    }

    /// <summary>
    /// 注入依赖，便于测试。
    /// </summary>
    public DefaultBrowserSwitcher(
        SettingsLauncher settingsLauncher,
        DefaultBrowserResolver defaultBrowserResolver,
        Func<string, string?> findRegisteredAppName,
        Func<string, bool> trySetAppAsDefault,
        Func<string, bool>? tryExternalSetDefaultBrowser = null,
        Func<string, bool>? tryLaunchAssociationUi = null)
    {
        _settingsLauncher = settingsLauncher
            ?? throw new ArgumentNullException(nameof(settingsLauncher));
        _defaultBrowserResolver = defaultBrowserResolver
            ?? throw new ArgumentNullException(nameof(defaultBrowserResolver));
        _findRegisteredAppName = findRegisteredAppName
            ?? throw new ArgumentNullException(nameof(findRegisteredAppName));
        _trySetAppAsDefault = trySetAppAsDefault
            ?? throw new ArgumentNullException(nameof(trySetAppAsDefault));
        _tryExternalSetDefaultBrowser = tryExternalSetDefaultBrowser
                                        ?? (_ => false);
        _tryLaunchAssociationUi = tryLaunchAssociationUi
                                  ?? (_ => false);
    }

    /// <summary>
    /// 构建指向某已注册应用的默认应用设置深链（微软官方文档推荐）。
    /// </summary>
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
    /// 社区工具 SetDefaultBrowser 命令行参数（按浏览器 Id）。
    /// </summary>
    public static string? BuildSetDefaultBrowserArgument(string browserId)
    {
        var attempts = ExternalAssociationTools.BuildSetDefaultBrowserArgumentAttempts(
            browserId);
        return attempts.Count > 0 ? attempts[0] : null;
    }

    internal static string? FindSetDefaultBrowserExecutable() =>
        ExternalAssociationTools.FindSetDefaultBrowserExecutable();

    /// <summary>
    /// 尝试切换默认浏览器。
    /// </summary>
    /// <param name="forceDirectSwitch">
    /// 个人本地模式：优先提权调用 SetUserFTA/SetDefaultBrowser，失败不自动打开设置页。
    /// </param>
    public BrowserSwitchResult Switch(
        BrowserInfo browser,
        IReadOnlyList<BrowserInfo> knownBrowsers,
        bool forceDirectSwitch = false)
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
                "未找到该浏览器，已打开系统默认应用页。");
        }

        var registeredName = _findRegisteredAppName(browser.Id)
                             ?? GuessRegisteredName(browser);

        if (forceDirectSwitch)
        {
            var forceResult = TryForceDirectSwitch(
                browser,
                knownBrowsers,
                registeredName);
            if (forceResult is not null)
            {
                return forceResult;
            }

            return BuildForceSwitchFailedResult(browser.DisplayName);
        }

        // --- 标准模式（合规回退设置 UI）---

        // 1) 本机若已安装社区工具 SetDefaultBrowser，优先尝试真正直切
        if (_tryExternalSetDefaultBrowser(browser.Id))
        {
            var afterExternal = _defaultBrowserResolver.ResolveCurrentDefaultId(
                knownBrowsers);
            if (afterExternal == browser.Id)
            {
                return BrowserSwitchResult.Switched(browser.DisplayName);
            }
        }

        // 2) 旧系统 COM（Win8+ 起官方已废弃写入；新系统多半被 UCPD 拦住）
        if (!string.IsNullOrWhiteSpace(registeredName)
            && _trySetAppAsDefault(registeredName))
        {
            var afterCom = _defaultBrowserResolver.ResolveCurrentDefaultId(
                knownBrowsers);
            if (afterCom == browser.Id)
            {
                return BrowserSwitchResult.Switched(browser.DisplayName);
            }
        }

        // 3) 官方路径 A：该应用的关联设置 UI（一点「设为默认」）
        if (!string.IsNullOrWhiteSpace(registeredName)
            && _tryLaunchAssociationUi(registeredName!))
        {
            return BrowserSwitchResult.FallbackToSettings(
                browser.DisplayName,
                $"已打开「{browser.DisplayName}」的系统关联页，请点击设为默认。"
                + "（系统禁止第三方静默改默认浏览器）");
        }

        // 4) 官方路径 B：Win11 深链到该应用默认应用页（微软文档）
        if (!string.IsNullOrWhiteSpace(registeredName)
            && TryOpenDeepLink(registeredName!))
        {
            return BrowserSwitchResult.FallbackToSettings(
                browser.DisplayName,
                $"已打开「{browser.DisplayName}」设置页，请点一次「设为默认」。"
                + "（这是微软允许的系统级入口）");
        }

        // 5) 总页回退
        _settingsLauncher.OpenDefaultApps();
        return BrowserSwitchResult.FallbackToSettings(
            browser.DisplayName,
            $"已打开系统默认应用页，请选择：{browser.DisplayName}");
    }

    private BrowserSwitchResult? TryForceDirectSwitch(
        BrowserInfo browser,
        IReadOnlyList<BrowserInfo> knownBrowsers,
        string? registeredName)
    {
        // 0) 内置 UserChoice 直写（个人本地，UCPD 未拦截时）
        if (UserChoiceAssociationWriter.TrySetDefaultBrowserById(browser.Id)
            && VerifyDefaultStable(knownBrowsers, browser.Id))
        {
            return BrowserSwitchResult.Switched(browser.DisplayName);
        }

        if (TryExternalToolSwitch(
                browser.Id,
                ExternalAssociationTools.FindSetUserFtaExecutable(),
                ExternalAssociationTools.BuildSetUserFtaArgumentAttempts(browser.Id),
                knownBrowsers,
                browser.Id))
        {
            return BrowserSwitchResult.Switched(browser.DisplayName);
        }

        if (TryExternalToolSwitch(
                browser.Id,
                ExternalAssociationTools.FindSetDefaultBrowserExecutable(),
                ExternalAssociationTools.BuildSetDefaultBrowserArgumentAttempts(browser.Id),
                knownBrowsers,
                browser.Id))
        {
            return BrowserSwitchResult.Switched(browser.DisplayName);
        }

        if (!string.IsNullOrWhiteSpace(registeredName)
            && _trySetAppAsDefault(registeredName))
        {
            var afterCom = _defaultBrowserResolver.ResolveCurrentDefaultId(knownBrowsers);
            if (afterCom == browser.Id)
            {
                return BrowserSwitchResult.Switched(browser.DisplayName);
            }
        }

        return null;
    }

    private bool TryExternalToolSwitch(
        string browserId,
        string? exe,
        IReadOnlyList<string> argumentAttempts,
        IReadOnlyList<BrowserInfo> knownBrowsers,
        string expectedBrowserId)
    {
        if (exe is null || argumentAttempts.Count == 0)
        {
            return false;
        }

        foreach (var args in argumentAttempts)
        {
            if (!AdminProcessLauncher.TryRun(exe, args))
            {
                continue;
            }

            if (_defaultBrowserResolver.ResolveCurrentDefaultId(knownBrowsers)
                == expectedBrowserId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 写入后立即校验，并在短延迟后再次校验，避免 Hash 错误导致“先成功后被系统回滚”。
    /// </summary>
    private bool VerifyDefaultStable(
        IReadOnlyList<BrowserInfo> knownBrowsers,
        string expectedBrowserId,
        int delayMs = 400)
    {
        if (_defaultBrowserResolver.ResolveCurrentDefaultId(knownBrowsers)
            != expectedBrowserId)
        {
            return false;
        }

        Thread.Sleep(delayMs);
        return _defaultBrowserResolver.ResolveCurrentDefaultId(knownBrowsers)
               == expectedBrowserId;
    }

    private static BrowserSwitchResult BuildForceSwitchFailedResult(string displayName)
    {
        var ucpd = UcpdManager.IsProtectionEnabled();
        var hasFta = ExternalAssociationTools.FindSetUserFtaExecutable() is not null;
        var hasLegacy = ExternalAssociationTools.FindSetDefaultBrowserExecutable() is not null;

        var hint = new List<string>();
        if (!hasFta && !hasLegacy)
        {
            hint.Add(
                "请将 SetUserFTA.exe 放到程序目录 Tools\\ 下（见 setuserfta.com，个人免费）。");
        }

        if (ucpd)
        {
            hint.Add(
                "本机 UCPD 仍在拦截 UserChoice 写入：可在设置中「禁用 UCPD」并重启，"
                + "或使用 SetUserFTA 2.1+。");
        }

        if (hasFta || hasLegacy)
        {
            hint.Add("工具已运行但未生效，请确认 UAC 已允许，并以管理员重试。");
        }

        return BrowserSwitchResult.ForceSwitchFailed(
            displayName,
            $"无法直切为 {displayName}。"
            + (hint.Count > 0 ? " " + string.Join(" ", hint) : string.Empty));
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
        // 机器级安装更常见，先 Machine 再 User（与微软示例一致）
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
                // 换另一种注册范围
            }
        }

        return false;
    }

    private static bool TryExternalSetDefaultBrowser(string browserId)
    {
        var arg = BuildSetDefaultBrowserArgument(browserId);
        var exe = FindSetDefaultBrowserExecutable();
        if (arg is null || exe is null)
        {
            return false;
        }

        try
        {
            // 不重定向标准流，避免管道塞满导致 WaitForExit 死锁；
            // 超时则结束子进程，避免与后续系统 UI 竞态。
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = arg,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process == null)
            {
                return false;
            }

            if (!process.WaitForExit(5000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // 进程已退出
                }
                catch (Exception)
                {
                    // 忽略强制结束失败，继续回退系统 UI
                }

                return false;
            }

            return process.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool TryLaunchAdvancedAssociationUi(string appRegistryName)
    {
        try
        {
            var type = Type.GetTypeFromCLSID(
                new Guid("1968106d-f3b5-44cf-890e-116fcb9ecef1"));
            if (type == null)
            {
                return false;
            }

            var com = Activator.CreateInstance(type);
            if (com is not IApplicationAssociationRegistrationUI ui)
            {
                return false;
            }

            ui.LaunchAdvancedAssociationUI(appRegistryName);
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

                // 宽松：名称包含候选关键字
                foreach (var name in key.GetValueNames())
                {
                    foreach (var candidate in candidates)
                    {
                        if (name.Contains(
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

    [ComImport]
    [Guid("1f76a169-f994-40ac-8fc8-0959e8874710")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationAssociationRegistrationUI
    {
        void LaunchAdvancedAssociationUI(
            [MarshalAs(UnmanagedType.LPWStr)] string appRegistryName);
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

    public BrowserSwitchOutcome Outcome { get; }

    public string BrowserName { get; }

    public string Message { get; }

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

    public static BrowserSwitchResult ForceSwitchFailed(string name, string message) =>
        new(BrowserSwitchOutcome.ForceSwitchFailed, name, message);
}

/// <summary>切换结果枚举。</summary>
public enum BrowserSwitchOutcome
{
    AlreadyDefault,
    Switched,
    FallbackToSettings,
    ForceSwitchFailed
}
