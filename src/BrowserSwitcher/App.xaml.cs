using BrowserSwitcher.Helpers;
using BrowserSwitcher.Models;
using BrowserSwitcher.ViewModels;
using Hardcodet.Wpf.TaskbarNotification;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;

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
        _trayIconHandle = (Icon)SystemIcons.Application.Clone();
        _trayIcon.Icon = _trayIconHandle;
        UpdateTrayTooltip();
        _trayIcon.TrayLeftMouseUp += (_, _) => ToggleMainWindow();
        // 自行弹出菜单并锚定到光标，避免多屏下 ContextMenu 错位
        _trayIcon.TrayRightMouseUp += (_, _) => ShowTrayContextMenu();
    }

    /// <summary>
    /// 在托盘图标旁（当前光标位置）弹出右键菜单。
    /// </summary>
    private void ShowTrayContextMenu()
    {
        if (_trayIcon == null || _viewModel == null)
        {
            return;
        }

        // 打开菜单前刷新，保证「当前默认」标记最新
        _viewModel.Refresh();
        UpdateTrayTooltip();

        var menu = new ContextMenu();

        foreach (BrowserInfo browser in _viewModel.Browsers)
        {
            var header = browser.IsCurrentDefault
                ? $"{browser.DisplayName}（当前）"
                : browser.DisplayName;
            var item = new MenuItem
            {
                Header = header,
                Tag = browser,
                IsChecked = browser.IsCurrentDefault,
                IsCheckable = browser.IsCurrentDefault
            };
            item.Click += BrowserMenuItem_Click;
            menu.Items.Add(item);
        }

        if (menu.Items.Count > 0)
        {
            menu.Items.Add(new Separator());
        }

        var openItem = new MenuItem { Header = "打开主窗口" };
        openItem.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(openItem);

        var exitItem = new MenuItem { Header = "退出" };
        exitItem.Click += (_, _) => ExitApplication();
        menu.Items.Add(exitItem);

        // 不挂到 TaskbarIcon.ContextMenu，避免库内定位跑到其他显示器
        _trayIcon.ContextMenu = null;
        TrayContextMenuPresenter.ShowNearCursor(menu, _mainWindow);
    }

    /// <summary>
    /// 更新托盘悬停提示，包含当前默认浏览器。
    /// </summary>
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

    /// <summary>
    /// 托盘菜单中选择浏览器：打开系统默认应用设置页。
    /// </summary>
    private void BrowserMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: BrowserInfo browser }
            && _viewModel != null)
        {
            _viewModel.SetAsDefault(browser);
            ShowMainWindow();
        }
    }

    /// <summary>
    /// 切换主窗口显示/隐藏。
    /// </summary>
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

    /// <summary>
    /// 显示并激活主窗口。
    /// </summary>
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

    /// <summary>
    /// 真正退出：允许关窗、释放 Mutex、销毁托盘并关闭应用。
    /// </summary>
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

    /// <summary>
    /// 释放托盘图标与单实例资源。
    /// </summary>
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
