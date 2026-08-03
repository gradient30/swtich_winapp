using System.Windows;
using System.Windows.Media;

namespace BrowserSwitcher.Services;

/// <summary>
/// 应用明/暗主题，并同步系统菜单色，保证菜单与正文对比度可读。
/// </summary>
public sealed class ThemeService
{
    /// <summary>暗色模式标识。</summary>
    public const string Dark = "Dark";

    /// <summary>亮色模式标识。</summary>
    public const string Light = "Light";

    /// <summary>
    /// 规范化主题名；非法值回退暗色。
    /// </summary>
    /// <param name="theme">配置中的主题。</param>
    /// <returns>Dark 或 Light。</returns>
    public static string Normalize(string? theme)
    {
        return string.Equals(theme, Light, StringComparison.OrdinalIgnoreCase)
            ? Light
            : Dark;
    }

    /// <summary>
    /// 将主题应用到 Application 资源。
    /// </summary>
    /// <param name="theme">Dark 或 Light。</param>
    public void Apply(string? theme)
    {
        var normalized = Normalize(theme);
        var colors = normalized == Light
            ? LightPalette()
            : DarkPalette();

        var resources = Application.Current?.Resources;
        if (resources == null)
        {
            return;
        }

        foreach (var pair in colors)
        {
            SetBrush(resources, pair.Key, pair.Value);
        }

        // 系统菜单键与业务 Brush 对齐，下拉菜单不再白底浅字
        SetBrush(resources, SystemColors.MenuBrushKey, colors["BrushBgElevated"]);
        SetBrush(resources, SystemColors.MenuBarBrushKey, colors["BrushBgPanel"]);
        SetBrush(resources, SystemColors.MenuTextBrushKey, colors["BrushTextPrimary"]);
        SetBrush(resources, SystemColors.HighlightBrushKey, colors["BrushAccent"]);
        SetBrush(resources, SystemColors.HighlightTextBrushKey, colors["BrushOnAccent"]);
        SetBrush(resources, SystemColors.ControlTextBrushKey, colors["BrushTextPrimary"]);
        SetBrush(resources, SystemColors.WindowTextBrushKey, colors["BrushTextPrimary"]);
        SetBrush(resources, SystemColors.GrayTextBrushKey, colors["BrushTextMuted"]);
    }

    private static void SetBrush(
        ResourceDictionary resources,
        object key,
        Color color)
    {
        // 始终替换为新画刷，避免修改 XAML 中可能被冻结的实例
        resources[key] = new SolidColorBrush(color);
    }

    private static Dictionary<string, Color> DarkPalette()
    {
        return new Dictionary<string, Color>
        {
            ["BrushBgDeep"] = ColorFrom("#16181C"),
            ["BrushBgPanel"] = ColorFrom("#1E2228"),
            ["BrushBgElevated"] = ColorFrom("#262B33"),
            ["BrushBgHover"] = ColorFrom("#2E3540"),
            ["BrushLineSubtle"] = ColorFrom("#3A424E"),
            ["BrushTextPrimary"] = ColorFrom("#F2EFE8"),
            ["BrushTextMuted"] = ColorFrom("#A8B0BB"),
            ["BrushAccent"] = ColorFrom("#0D9488"),
            ["BrushAccentSoft"] = Color.FromArgb(0x33, 0x0D, 0x94, 0x88),
            ["BrushOnAccent"] = Colors.White
        };
    }

    private static Dictionary<string, Color> LightPalette()
    {
        return new Dictionary<string, Color>
        {
            ["BrushBgDeep"] = ColorFrom("#F4F1EA"),
            ["BrushBgPanel"] = ColorFrom("#FFFFFF"),
            ["BrushBgElevated"] = ColorFrom("#EEF1F4"),
            ["BrushBgHover"] = ColorFrom("#E4E9EF"),
            ["BrushLineSubtle"] = ColorFrom("#D0D7DE"),
            ["BrushTextPrimary"] = ColorFrom("#1C2128"),
            ["BrushTextMuted"] = ColorFrom("#5B6570"),
            ["BrushAccent"] = ColorFrom("#0F766E"),
            ["BrushAccentSoft"] = Color.FromArgb(0x22, 0x0F, 0x76, 0x6E),
            ["BrushOnAccent"] = Colors.White
        };
    }

    private static Color ColorFrom(string hex)
    {
        return (Color)ColorConverter.ConvertFromString(hex)!;
    }
}
