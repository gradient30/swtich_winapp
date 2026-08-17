namespace BrowserSwitcher.Models;

public sealed class AppConfig
{
    public Dictionary<string, string> ManualPaths { get; set; } = new();
    public bool StartWithWindows { get; set; }

    /// <summary>
    /// 界面主题：Dark 或 Light。
    /// </summary>
    public string Theme { get; set; } = "Dark";

    /// <summary>
    /// 个人本地：托盘一键直切（提权 + SetUserFTA/SetDefaultBrowser），失败不打开设置页。
    /// </summary>
    public bool ForceDirectSwitch { get; set; } = true;
}

