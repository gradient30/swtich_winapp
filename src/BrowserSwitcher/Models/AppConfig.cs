namespace BrowserSwitcher.Models;

public sealed class AppConfig
{
    public Dictionary<string, string> ManualPaths { get; set; } = new();
    public bool StartWithWindows { get; set; }

    /// <summary>
    /// 界面主题：Dark 或 Light。
    /// </summary>
    public string Theme { get; set; } = "Dark";
}

