using System.IO;
using System.Text.RegularExpressions;
using BrowserSwitcher.Models;
using Microsoft.Win32;

namespace BrowserSwitcher.Services;

/// <summary>
/// 读取系统当前默认 HTTP 浏览器（UserChoice ProgId），并映射到已知浏览器。
/// </summary>
public sealed class DefaultBrowserResolver
{
    private static readonly Regex ExeInCommand = new(
        @"""(?<path>[^""]+\.exe)""|(?<path>[^\s""]+\.exe)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly Func<string?> _readHttpProgId;
    private readonly Func<string, string?> _readProgIdCommand;

    /// <summary>
    /// 使用真实注册表读取器。
    /// </summary>
    public DefaultBrowserResolver()
        : this(ReadHttpProgIdFromRegistry, ReadProgIdCommandFromRegistry)
    {
    }

    /// <summary>
    /// 注入读取器，便于单元测试。
    /// </summary>
    /// <param name="readHttpProgId">读取 http UserChoice ProgId。</param>
    /// <param name="readProgIdCommand">按 ProgId 读取 open\command。</param>
    public DefaultBrowserResolver(
        Func<string?> readHttpProgId,
        Func<string, string?> readProgIdCommand)
    {
        _readHttpProgId = readHttpProgId
            ?? throw new ArgumentNullException(nameof(readHttpProgId));
        _readProgIdCommand = readProgIdCommand
            ?? throw new ArgumentNullException(nameof(readProgIdCommand));
    }

    /// <summary>
    /// 在已检测浏览器列表中找出当前系统默认项的 Id。
    /// </summary>
    /// <param name="browsers">已检测浏览器。</param>
    /// <returns>匹配到的浏览器 Id；无法识别时为 null。</returns>
    public string? ResolveCurrentDefaultId(IReadOnlyList<BrowserInfo> browsers)
    {
        ArgumentNullException.ThrowIfNull(browsers);

        var progId = _readHttpProgId();
        if (string.IsNullOrWhiteSpace(progId))
        {
            return null;
        }

        var byProgId = MatchByProgId(progId, browsers);
        if (byProgId is not null)
        {
            return byProgId;
        }

        var command = _readProgIdCommand(progId);
        var exePath = ExtractExecutablePath(command);
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return null;
        }

        return MatchByExecutablePath(exePath, browsers);
    }

    /// <summary>
    /// 从命令行字符串中提取 .exe 路径。
    /// </summary>
    /// <param name="command">注册表 open\command 值。</param>
    /// <returns>可执行路径；无法解析时为 null。</returns>
    public static string? ExtractExecutablePath(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        var match = ExeInCommand.Match(command);
        if (!match.Success)
        {
            return null;
        }

        var path = Environment.ExpandEnvironmentVariables(
            match.Groups["path"].Value.Trim());
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    private static string? MatchByProgId(
        string progId,
        IReadOnlyList<BrowserInfo> browsers)
    {
        var normalized = progId.Trim();
        string? id = null;

        if (normalized.StartsWith("ChromeHTML", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Chrome", StringComparison.OrdinalIgnoreCase)
               && normalized.Contains("HTML", StringComparison.OrdinalIgnoreCase))
        {
            id = "chrome";
        }
        else if (normalized.StartsWith("BraveHTML", StringComparison.OrdinalIgnoreCase)
                 || normalized.StartsWith("Brave", StringComparison.OrdinalIgnoreCase)
                    && normalized.Contains("HTML", StringComparison.OrdinalIgnoreCase))
        {
            id = "brave";
        }
        else if (normalized.StartsWith("MSEdgeHTM", StringComparison.OrdinalIgnoreCase)
                 || normalized.StartsWith("AppX", StringComparison.OrdinalIgnoreCase)
                    && normalized.Contains("MicrosoftEdge", StringComparison.OrdinalIgnoreCase))
        {
            id = "edge";
        }
        else if (normalized.StartsWith("IE.HTTP", StringComparison.OrdinalIgnoreCase)
                 || normalized.Equals("IE.HTTPS", StringComparison.OrdinalIgnoreCase)
                 || normalized.Equals("htmlfile", StringComparison.OrdinalIgnoreCase))
        {
            id = "ie";
        }

        return id is null
            ? null
            : browsers.FirstOrDefault(b => b.Id == id)?.Id;
    }

    private static string? MatchByExecutablePath(
        string exePath,
        IReadOnlyList<BrowserInfo> browsers)
    {
        var fullPath = Path.GetFullPath(exePath);
        var fileName = Path.GetFileName(fullPath);

        foreach (var browser in browsers)
        {
            if (string.IsNullOrWhiteSpace(browser.ExecutablePath))
            {
                continue;
            }

            try
            {
                var browserPath = Path.GetFullPath(browser.ExecutablePath);
                if (string.Equals(
                        browserPath,
                        fullPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return browser.Id;
                }
            }
            catch (Exception)
            {
                // 非法路径时退回文件名比较
            }

            if (string.Equals(
                    Path.GetFileName(browser.ExecutablePath),
                    fileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return browser.Id;
            }
        }

        return null;
    }

    private static string? ReadHttpProgIdFromRegistry()
    {
        const string keyPath =
            @"Software\Microsoft\Windows\Shell\Associations\"
            + @"UrlAssociations\http\UserChoice";

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath);
            return key?.GetValue("ProgId") as string;
        }
        catch (System.Security.SecurityException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string? ReadProgIdCommandFromRegistry(string progId)
    {
        if (string.IsNullOrWhiteSpace(progId))
        {
            return null;
        }

        var keyPath = progId.Trim() + @"\shell\open\command";
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(keyPath);
            return key?.GetValue(null) as string;
        }
        catch (System.Security.SecurityException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
