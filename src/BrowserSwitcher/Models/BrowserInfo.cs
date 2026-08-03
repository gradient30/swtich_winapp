namespace BrowserSwitcher.Models;

public enum BrowserStatus
{
    Installed,
    Manual,
    NotFound
}

/// <summary>
/// 单个浏览器的检测结果。
/// </summary>
public sealed class BrowserInfo
{
    /// <summary>内部标识，如 chrome。</summary>
    public required string Id { get; init; }

    /// <summary>界面显示名称。</summary>
    public required string DisplayName { get; init; }

    /// <summary>可执行文件路径；未找到时为 null。</summary>
    public string? ExecutablePath { get; init; }

    /// <summary>安装/手动/未找到状态。</summary>
    public BrowserStatus Status { get; init; }

    /// <summary>是否为系统当前默认 HTTP 浏览器。</summary>
    public bool IsCurrentDefault { get; init; }

    /// <summary>列表副标题：状态 + 是否当前默认。</summary>
    public string StatusDisplay
    {
        get
        {
            var statusText = Status switch
            {
                BrowserStatus.Installed => "已安装",
                BrowserStatus.Manual => "手动指定",
                BrowserStatus.NotFound => "未找到",
                _ => "未知"
            };

            return IsCurrentDefault
                ? $"状态：{statusText} · 当前默认"
                : $"状态：{statusText}";
        }
    }
}
