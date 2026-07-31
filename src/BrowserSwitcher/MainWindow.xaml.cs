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
    /// 主窗口视图模型（托盘菜单与主界面共用）。
    /// </summary>
    public MainViewModel ViewModel => _viewModel;

    /// <summary>
    /// 为 true 时允许真正关闭窗口（托盘「退出」时设置）。
    /// </summary>
    public bool AllowClose { get; set; }

    /// <summary>
    /// 关闭时默认隐藏到托盘；仅当 <see cref="AllowClose"/> 为 true 时退出。
    /// </summary>
    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (AllowClose)
        {
            return;
        }

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
