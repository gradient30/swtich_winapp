using System.Windows.Controls;
using System.Windows.Input;
using BrowserSwitcher.ViewModels;

namespace BrowserSwitcher.Views;

/// <summary>
/// 设置页：居中紧凑卡片，含手动路径与开机启动。
/// </summary>
public partial class SettingsView : UserControl
{
    /// <summary>
    /// 初始化设置页。
    /// </summary>
    public SettingsView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 点击遮罩关闭设置面板。
    /// </summary>
    private void Dimmer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm
            && vm.BackToMainCommand.CanExecute(null))
        {
            vm.BackToMainCommand.Execute(null);
        }

        e.Handled = true;
    }
}
