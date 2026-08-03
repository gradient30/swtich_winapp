using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BrowserSwitcher;

/// <summary>
/// 简要帮助与本次更新说明对话框。
/// </summary>
public sealed class HelpWindow : Window
{
    /// <summary>
    /// 创建帮助窗口。
    /// </summary>
    /// <param name="appVersion">程序版本号。</param>
    /// <param name="helpText">已格式化的说明正文。</param>
    public HelpWindow(string appVersion, string helpText)
    {
        Title = "帮助";
        Width = 440;
        Height = 420;
        MinWidth = 360;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        Background = BrushOrFallback("BrushBgDeep", Brushes.Black);
        Foreground = BrushOrFallback("BrushTextPrimary", Brushes.White);

        var root = new DockPanel { Margin = new Thickness(18) };

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var closeButton = new Button
        {
            Content = "关闭",
            IsCancel = true,
            Padding = new Thickness(14, 7, 14, 7),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        if (TryFindResource("GhostButton") is Style ghost)
        {
            closeButton.Style = ghost;
        }

        closeButton.Click += (_, _) => Close();
        DockPanel.SetDock(closeButton, Dock.Right);
        header.Children.Add(closeButton);
        header.Children.Add(new TextBlock
        {
            Text = $"浏览器切换器  v{appVersion}",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var body = new TextBlock
        {
            Text = helpText,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 22,
            Foreground = BrushOrFallback("BrushTextPrimary", Brushes.White)
        };
        root.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = body
        });

        Content = root;
    }

    private static Brush BrushOrFallback(string key, Brush fallback)
    {
        return Application.Current?.TryFindResource(key) as Brush ?? fallback;
    }
}
