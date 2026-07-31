using System.ComponentModel;
using System.Windows;
using BrowserSwitcher.Models;
using BrowserSwitcher.ViewModels;
using Microsoft.Win32;

namespace BrowserSwitcher;

/// <summary>
/// 主窗口：浏览器列表、设为默认与指定路径。
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    /// <summary>
    /// 初始化主窗口并加载浏览器列表。
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        _viewModel.Refresh();
    }

    /// <summary>
    /// 关闭时隐藏到后台，不退出进程（托盘任务再接真正退出）。
    /// </summary>
    private void Window_Closing(object sender, CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    /// <summary>
    /// 通过文件对话框为浏览器指定可执行路径。
    /// </summary>
    private void AssignPath_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: BrowserInfo browser })
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "可执行文件 (*.exe)|*.exe",
            Title = $"指定 {browser.DisplayName} 路径"
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.AssignPath(browser, dialog.FileName);
        }
    }
}
