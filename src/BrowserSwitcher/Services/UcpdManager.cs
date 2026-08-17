using Microsoft.Win32;

namespace BrowserSwitcher.Services;

/// <summary>
/// 检测 / 禁用 UCPD（User Choice Protection Driver）。个人本地维护用。
/// </summary>
public static class UcpdManager
{
    private const string UcpdServiceKey =
        @"SYSTEM\CurrentControlSet\Services\UCPD";

    /// <summary>
    /// UCPD 服务是否处于启用（非 Disabled）状态。
    /// </summary>
    public static bool IsProtectionEnabled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(UcpdServiceKey);
            if (key?.GetValue("Start") is not int start)
            {
                // 无键或未安装驱动，视为未拦截
                return false;
            }

            // 4 = SERVICE_DISABLED
            return start != 4;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// 尝试以管理员禁用 UCPD 服务并停用 velocity 计划任务。需重启后生效。
    /// </summary>
    public static bool TryDisableProtection()
    {
        const string cmd =
            "sc.exe config UCPD start= disabled"
            + " && schtasks.exe /change /Disable /TN "
            + "\"\\Microsoft\\Windows\\AppxDeploymentClient\\UCPD velocity\"";

        return AdminProcessLauncher.TryRunCmdAsAdmin(cmd);
    }

    /// <summary>
    /// 面向用户的状态说明。
    /// </summary>
    public static string DescribeStatus()
    {
        return IsProtectionEnabled()
            ? "UCPD 已启用：第三方直写 UserChoice 会被内核拦截。请安装 SetUserFTA 2.1+ 或禁用 UCPD 后重启。"
            : "UCPD 已禁用或未加载：可使用 SetUserFTA / SetDefaultBrowser 直切。";
    }
}
