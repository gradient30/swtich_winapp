using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace BrowserSwitcher.Services;

/// <summary>
/// 以管理员身份启动外部进程（个人本地一键直切用）。
/// </summary>
public static class AdminProcessLauncher
{
    /// <summary>
    /// 当前进程是否已具备管理员权限。
    /// </summary>
    public static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// 启动进程；若未提权则弹出 UAC（Verb=runas）。
    /// </summary>
    /// <param name="exePath">可执行文件路径。</param>
    /// <param name="arguments">命令行参数。</param>
    /// <param name="timeoutMs">等待退出毫秒；≤0 则不等待。</param>
    /// <returns>进程已启动且（若等待）退出码为 0。</returns>
    public static bool TryRun(
        string exePath,
        string arguments,
        int timeoutMs = 15000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);

        try
        {
            using var process = Process.Start(BuildStartInfo(exePath, arguments));
            if (process == null)
            {
                return false;
            }

            if (timeoutMs <= 0)
            {
                return true;
            }

            if (!process.WaitForExit(timeoutMs))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception)
                {
                    // 忽略
                }

                return false;
            }

            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // 用户取消 UAC 等
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// 以管理员运行 cmd.exe /c 命令（用于禁用 UCPD 等一次性维护）。
    /// </summary>
    public static bool TryRunCmdAsAdmin(string commandLine, int timeoutMs = 60000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandLine);
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var cmd = System.IO.Path.Combine(systemRoot, "cmd.exe");
        return TryRun(cmd, $"/c {commandLine}", timeoutMs);
    }

    internal static ProcessStartInfo BuildStartInfo(string exePath, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        if (!IsRunningAsAdministrator())
        {
            psi.Verb = "runas";
        }

        return psi;
    }
}
