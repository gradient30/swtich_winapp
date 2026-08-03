using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using BrowserSwitcher.Helpers;
using BrowserSwitcher.Models;
using BrowserSwitcher.ViewModels;
using Hardcodet.Wpf.TaskbarNotification;

namespace BrowserSwitcher;

/// <summary>
/// 应用入口：单实例、托盘常驻与主窗口生命周期。
/// </summary>
public partial class App : Application
{
    private SingleInstance? _singleInstance;
    private TaskbarIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private MainViewModel? _viewModel;
    private Icon? _trayIconHandle;
    private bool _isExiting;

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 尽早应用主题，避免首帧菜单/正文对比度错误
        try
        {
            var theme = new Services.ConfigStore().Load().Theme;
            new Services.ThemeService().Apply(theme);
        }
        catch (Exception)
        {
            new Services.ThemeService().Apply(Services.ThemeService.Dark);
        }

        try
        {
            _singleInstance = SingleInstance.Create();
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(
                ex.Message,
                "浏览器切换器",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
            return;
        }

        if (!_singleInstance.IsPrimary)
        {
            _singleInstance.SignalShowWindow();
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }

        // 注销/关机时允许真正退出，避免托盘残留
        SessionEnding += (_, _) =>
        {
            if (_mainWindow != null)
            {
                _mainWindow.AllowClose = true;
            }

            _isExiting = true;
        };

        _mainWindow = new MainWindow();
        _viewModel = _mainWindow.ViewModel;
        MainWindow = _mainWindow;

        // 先 Show 再 Hide，确保窗口完成初始化；默认仅托盘可见
        _mainWindow.Show();
        _mainWindow.Hide();

        InitializeTrayIcon();

        // BeginInvoke 避免退出时 Dispose/Join 与同步 Invoke 死锁
        _singleInstance.BeginWaitForShowSignal(
            () => Dispatcher.BeginInvoke(ShowMainWindow));
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        CleanupTrayAndSingleInstance();
        base.OnExit(e);
    }

    /// <summary>
    /// 初始化托盘图标、左键切换与动态右键菜单。
    /// </summary>
    private void InitializeTrayIcon()
    {
        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
        _trayIconHandle = LoadTrayIcon() ?? (Icon)SystemIcons.Application.Clone();
        _trayIcon.Icon = _trayIconHandle;
        UpdateTrayTooltip();
        _trayIcon.TrayLeftMouseUp += (_, _) => ToggleMainWindow();
        _trayIcon.TrayRightMouseUp += (_, _) => ShowTrayContextMenu();
    }

    /// <summary>
    /// 加载嵌入的切换开关图标。
    /// </summary>
    private static Icon? LoadTrayIcon()
    {
        try
        {
            var uri = new Uri(
                "pack://application:,,,/Resources/tray.ico",
                UriKind.Absolute);
            var streamInfo = GetResourceStream(uri);
            if (streamInfo?.Stream == null)
            {
                return null;
            }

            return new Icon(streamInfo.Stream);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 在托盘图标旁弹出右键菜单。
    /// </summary>
    private void ShowTrayContextMenu()
    {
        if (_trayIcon == null || _viewModel == null)
        {
            return;
        }

        _viewModel.Refresh();
        UpdateTrayTooltip();

        var menu = new ContextMenu();
        ApplyTrayMenuTheme(menu);

        foreach (BrowserInfo browser in _viewModel.Browsers)
        {
            var header = browser.IsCurrentDefault
                ? $"{browser.DisplayName}（当前）"
                : $"切换到 {browser.DisplayName}";
            var item = new MenuItem
            {
                Header = header,
                Tag = browser,
                IsChecked = browser.IsCurrentDefault,
                IsCheckable = false,
                IsEnabled = browser.Status != BrowserStatus.NotFound
                            || browser.IsCurrentDefault
            };
            ApplyTrayMenuItemTheme(item);
            item.Click += BrowserMenuItem_Click;
            menu.Items.Add(item);
        }

        if (menu.Items.Count > 0)
        {
            var sep = new Separator();
            if (TryFindResource("ThemedMenuSeparator") is Style sepStyle)
            {
                sep.Style = sepStyle;
            }

            menu.Items.Add(sep);
        }

        var openItem = new MenuItem { Header = "打开主窗口" };
        ApplyTrayMenuItemTheme(openItem);
        openItem.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(openItem);

        var exitItem = new MenuItem { Header = "退出" };
        ApplyTrayMenuItemTheme(exitItem);
        exitItem.Click += (_, _) => ExitApplication();
        menu.Items.Add(exitItem);

        _trayIcon.ContextMenu = null;
        TrayContextMenuPresenter.ShowNearCursor(menu, _mainWindow);
    }

    /// <summary>
    /// 为托盘 ContextMenu 套用自绘主题样式（避免系统白底白字）。
    /// </summary>
    private void ApplyTrayMenuTheme(ContextMenu menu)
    {
        if (TryFindResource("ThemedContextMenu") is Style menuStyle)
        {
            menu.Style = menuStyle;
        }

        if (TryFindResource("ThemedTrayMenuItem") is Style itemStyle)
        {
            menu.Resources[typeof(MenuItem)] = itemStyle;
        }

        if (TryFindResource("BrushBgElevated") is System.Windows.Media.Brush bg)
        {
            menu.Background = bg;
        }

        if (TryFindResource("BrushTextPrimary") is System.Windows.Media.Brush fg)
        {
            menu.Foreground = fg;
        }

        if (TryFindResource("BrushLineSubtle") is System.Windows.Media.Brush border)
        {
            menu.BorderBrush = border;
        }
    }

    /// <summary>
    /// 为单个托盘菜单项套用主题前景/样式。
    /// </summary>
    private void ApplyTrayMenuItemTheme(MenuItem item)
    {
        if (TryFindResource("ThemedTrayMenuItem") is Style itemStyle)
        {
            item.Style = itemStyle;
        }

        if (TryFindResource("BrushTextPrimary") is System.Windows.Media.Brush fg)
        {
            item.Foreground = fg;
        }
    }

    private void UpdateTrayTooltip()
    {
        if (_trayIcon == null)
        {
            return;
        }

        if (_viewModel?.CurrentDefaultDisplayName is { } name)
        {
            _trayIcon.ToolTipText = $"浏览器切换器\n当前默认：{name}";
        }
        else
        {
            _trayIcon.ToolTipText = "浏览器切换器\n当前默认：未识别";
        }
    }

    private void BrowserMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: BrowserInfo browser }
            || _viewModel == null)
        {
            return;
        }

        if (browser.IsCurrentDefault)
        {
            ShowMainWindow();
            return;
        }

        _viewModel.SwitchFromTray(browser);
        UpdateTrayTooltip();

        if (_viewModel.Browsers.Any(b => b.Id == browser.Id && b.IsCurrentDefault))
        {
            _trayIcon?.ShowBalloonTip(
                "浏览器切换器",
                $"已切换为 {browser.DisplayName}",
                BalloonIcon.Info);
        }
        else
        {
            ShowMainWindow();
        }
    }

    private void ToggleMainWindow()
    {
        if (_mainWindow == null)
        {
            return;
        }

        if (_mainWindow.IsVisible)
        {
            _mainWindow.Hide();
        }
        else
        {
            ShowMainWindow();
        }
    }

    private void ShowMainWindow()
    {
        if (_isExiting
            || Dispatcher.HasShutdownStarted
            || _mainWindow == null)
        {
            return;
        }

        if (!_mainWindow.IsVisible)
        {
            _mainWindow.Show();
        }

        _viewModel?.Refresh();
        UpdateTrayTooltip();

        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.Activate();
        _mainWindow.Topmost = true;
        _mainWindow.Topmost = false;
        _mainWindow.Focus();
    }

    private void ExitApplication()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;

        if (_mainWindow != null)
        {
            _mainWindow.AllowClose = true;
            _mainWindow.Close();
        }

        CleanupTrayAndSingleInstance();
        Shutdown();
    }

    private void CleanupTrayAndSingleInstance()
    {
        if (_trayIcon != null)
        {
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        if (_trayIconHandle != null)
        {
            _trayIconHandle.Dispose();
            _trayIconHandle = null;
        }

        if (_singleInstance != null)
        {
            _singleInstance.Dispose();
            _singleInstance = null;
        }
    }
}
