using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BrowserSwitcher.Models;

/// <summary>
/// 设置页中可编辑的手动路径条目。
/// </summary>
public sealed class ManualPathEntry : INotifyPropertyChanged
{
    private string _displayName = string.Empty;
    private string _path = string.Empty;

    /// <summary>浏览器显示名称（对应 ManualPaths 字典键）。</summary>
    public string DisplayName
    {
        get => _displayName;
        set => SetField(ref _displayName, value);
    }

    /// <summary>可执行文件路径。</summary>
    public string Path
    {
        get => _path;
        set => SetField(ref _path, value);
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
