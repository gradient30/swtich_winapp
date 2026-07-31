namespace BrowserSwitcher.Models;

public sealed class AppConfig
{
    public Dictionary<string, string> ManualPaths { get; set; } = new();
    public bool StartWithWindows { get; set; }
}
