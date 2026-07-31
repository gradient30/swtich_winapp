using System.Windows.Input;

namespace BrowserSwitcher.ViewModels;

/// <summary>
/// 轻量 ICommand 实现，供主窗口按钮绑定。
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    /// <summary>
    /// 创建可执行命令。
    /// </summary>
    /// <param name="execute">执行委托。</param>
    /// <param name="canExecute">可选的可执行判断。</param>
    public RelayCommand(
        Action<object?> execute,
        Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <inheritdoc />
    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke(parameter) ?? true;
    }

    /// <inheritdoc />
    public void Execute(object? parameter)
    {
        _execute(parameter);
    }

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
