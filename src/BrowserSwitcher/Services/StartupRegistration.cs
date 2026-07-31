using Microsoft.Win32;

namespace BrowserSwitcher.Services;

/// <summary>
/// 通过当前用户 Run 注册表项控制开机启动。
/// </summary>
public sealed class StartupRegistration
{
    /// <summary>默认写入的 Run 值名称。</summary>
    public const string DefaultValueName = "BrowserSwitcher";

    /// <summary>当前用户 Run 子键路径。</summary>
    public const string RunSubKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly Func<bool, RegistryKey?> _openRunKey;
    private readonly string _valueName;

    /// <summary>
    /// 使用当前用户 Run 键创建注册器。
    /// </summary>
    /// <param name="openRunKey">
    /// 打开 Run 键的工厂；参数为是否可写。测试可注入临时键。
    /// </param>
    /// <param name="valueName">Run 值名称。</param>
    public StartupRegistration(
        Func<bool, RegistryKey?>? openRunKey = null,
        string valueName = DefaultValueName)
    {
        _openRunKey = openRunKey ?? OpenCurrentUserRunKey;
        _valueName = string.IsNullOrWhiteSpace(valueName)
            ? DefaultValueName
            : valueName;
    }

    /// <summary>
    /// 判断 Run 键中是否已注册本应用。
    /// </summary>
    /// <returns>已注册且值非空时为 true。</returns>
    public bool IsRegistered()
    {
        using var key = _openRunKey(false);
        var value = key?.GetValue(_valueName) as string;
        return !string.IsNullOrWhiteSpace(value);
    }

    /// <summary>
    /// 启用或禁用开机启动。
    /// </summary>
    /// <param name="enabled">是否开机启动。</param>
    /// <param name="executablePath">可执行文件完整路径（启用时必填）。</param>
    /// <exception cref="ArgumentException">启用时路径为空。</exception>
    /// <exception cref="InvalidOperationException">无法打开 Run 键。</exception>
    public void Apply(bool enabled, string executablePath)
    {
        if (enabled)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                throw new ArgumentException(
                    "启用开机启动时必须提供可执行路径。",
                    nameof(executablePath));
            }

            using var key = _openRunKey(true)
                ?? throw new InvalidOperationException(
                    "无法打开当前用户 Run 注册表项。");
            key.SetValue(_valueName, QuotePath(executablePath));
            return;
        }

        using var keyToClear = _openRunKey(true);
        keyToClear?.DeleteValue(_valueName, throwOnMissingValue: false);
    }

    /// <summary>
    /// 获取当前进程可执行文件路径。
    /// </summary>
    /// <returns>进程路径。</returns>
    /// <exception cref="InvalidOperationException">无法解析路径。</exception>
    public static string GetCurrentExecutablePath()
    {
        return Environment.ProcessPath
            ?? throw new InvalidOperationException(
                "无法解析当前进程可执行路径。");
    }

    private static RegistryKey? OpenCurrentUserRunKey(bool writable)
    {
        var existing = Registry.CurrentUser.OpenSubKey(
            RunSubKeyPath,
            writable);
        if (existing != null)
        {
            return existing;
        }

        return writable
            ? Registry.CurrentUser.CreateSubKey(RunSubKeyPath)
            : null;
    }

    private static string QuotePath(string path)
    {
        var trimmed = path.Trim();
        if (trimmed.StartsWith('"') && trimmed.EndsWith('"'))
        {
            return trimmed;
        }

        return $"\"{trimmed}\"";
    }
}
