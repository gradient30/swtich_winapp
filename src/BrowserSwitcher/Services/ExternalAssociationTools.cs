using System.IO;

namespace BrowserSwitcher.Services;

/// <summary>
/// 定位本机 SetUserFTA / SetDefaultBrowser（不随应用分发，需用户自行放入 Tools）。
/// </summary>
public static class ExternalAssociationTools
{
    /// <summary>
    /// 查找 SetUserFTA.exe（UCPD 时代推荐，见 setuserfta.com）。
    /// </summary>
    public static string? FindSetUserFtaExecutable()
    {
        return FindInAppDirectory("SetUserFTA.exe");
    }

    /// <summary>
    /// 查找 SetDefaultBrowser.exe（旧版社区工具）。
    /// </summary>
    public static string? FindSetDefaultBrowserExecutable()
    {
        return FindInAppDirectory("SetDefaultBrowser.exe");
    }

    /// <summary>
    /// 为指定浏览器生成 SetUserFTA 参数尝试列表（按成功率排序）。
    /// </summary>
    public static IReadOnlyList<string> BuildSetUserFtaArgumentAttempts(string browserId)
    {
        return browserId switch
        {
            "chrome" => new[]
            {
                "http,https ChromeHTML",
                "set http,https ChromeHTML",
                "HKLM \"Google Chrome\""
            },
            "edge" => new[]
            {
                "http,https MSEdgeHTM",
                "set http,https MSEdgeHTM",
                "HKLM \"Microsoft Edge\""
            },
            "brave" => new[]
            {
                "http,https BraveHTML",
                "set http,https BraveHTML",
                "HKLM Brave"
            },
            "ie" => new[]
            {
                "http,https IE.HTTP",
                "set http,https IE.HTTP",
                "ie"
            },
            _ => Array.Empty<string>()
        };
    }

    /// <summary>
    /// SetDefaultBrowser 快捷参数。
    /// </summary>
    public static IReadOnlyList<string> BuildSetDefaultBrowserArgumentAttempts(string browserId)
    {
        return browserId switch
        {
            "chrome" => new[] { "chrome" },
            "edge" => new[] { "edge" },
            "brave" => new[] { "HKLM Brave" },
            "ie" => new[] { "ie" },
            _ => Array.Empty<string>()
        };
    }

    private static string? FindInAppDirectory(string fileName)
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            System.IO.Path.Combine(baseDir, "Tools", fileName),
            System.IO.Path.Combine(baseDir, fileName)
        };

        return candidates.FirstOrDefault(System.IO.File.Exists);
    }
}
