using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using BrowserSwitcher.Models;
using BrowserSwitcher.Services;

namespace BrowserSwitcher.ViewModels;

/// <summary>
/// 主窗口视图模型：浏览器列表、设为默认、指定路径与设置页。
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ConfigStore _configStore;
    private readonly BrowserDetector _detector;
    private readonly SettingsLauncher _settingsLauncher;
    private readonly StartupRegistration _startupRegistration;
    private string _statusMessage = string.Empty;
    private bool _isSettingsVisible;
    private bool _startWithWindows;
    private bool _suppressStartupApply;

    /// <summary>
    /// 使用默认服务构造主视图模型。
    /// </summary>
    public MainViewModel()
        : this(
            new ConfigStore(),
            new BrowserDetector(),
            new SettingsLauncher(),
            new StartupRegistration())
    {
    }

    /// <summary>
    /// 注入依赖，便于测试。
    /// </summary>
    /// <param name="configStore">配置读写。</param>
    /// <param name="detector">浏览器检测。</param>
    /// <param name="settingsLauncher">系统默认应用设置页启动器。</param>
    /// <param name="startupRegistration">开机启动注册。</param>
    public MainViewModel(
        ConfigStore configStore,
        BrowserDetector detector,
        SettingsLauncher settingsLauncher,
        StartupRegistration startupRegistration)
    {
        _configStore = configStore
            ?? throw new ArgumentNullException(nameof(configStore));
        _detector = detector
            ?? throw new ArgumentNullException(nameof(detector));
        _settingsLauncher = settingsLauncher
            ?? throw new ArgumentNullException(nameof(settingsLauncher));
        _startupRegistration = startupRegistration
            ?? throw new ArgumentNullException(nameof(startupRegistration));

        AppVersion = ResolveAppVersion();

        RefreshCommand = new RelayCommand(_ => Refresh());
        SetAsDefaultCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is BrowserInfo browser)
                {
                    SetAsDefault(browser);
                }
            });
        OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
        BackToMainCommand = new RelayCommand(_ => IsSettingsVisible = false);
        SaveManualPathsCommand = new RelayCommand(_ => SaveManualPaths());
        RemoveManualPathCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is ManualPathEntry entry)
                {
                    RemoveManualPath(entry);
                }
            });
    }

    /// <summary>当前检测到的浏览器列表。</summary>
    public ObservableCollection<BrowserInfo> Browsers { get; } = new();

    /// <summary>设置页中的手动路径编辑列表。</summary>
    public ObservableCollection<ManualPathEntry> ManualPathEntries { get; }
        = new();

    /// <summary>底部状态提示文本。</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    /// <summary>是否显示设置页。</summary>
    public bool IsSettingsVisible
    {
        get => _isSettingsVisible;
        private set => SetField(ref _isSettingsVisible, value);
    }

    /// <summary>
    /// 是否开机启动；变更时写入 HKCU Run 并持久化配置。
    /// </summary>
    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (!SetField(ref _startWithWindows, value))
            {
                return;
            }

            if (!_suppressStartupApply)
            {
                ApplyStartWithWindows(value);
            }
        }
    }

    /// <summary>程序集版本号显示文本。</summary>
    public string AppVersion { get; }

    /// <summary>刷新浏览器列表。</summary>
    public ICommand RefreshCommand { get; }

    /// <summary>打开系统默认应用页并提示选择指定浏览器。</summary>
    public ICommand SetAsDefaultCommand { get; }

    /// <summary>切换到设置页。</summary>
    public ICommand OpenSettingsCommand { get; }

    /// <summary>从设置页返回主列表。</summary>
    public ICommand BackToMainCommand { get; }

    /// <summary>保存手动路径到配置并刷新列表。</summary>
    public ICommand SaveManualPathsCommand { get; }

    /// <summary>从编辑列表移除一条手动路径。</summary>
    public ICommand RemoveManualPathCommand { get; }

    /// <summary>
    /// 加载配置、检测浏览器并更新集合。
    /// </summary>
    public void Refresh()
    {
        try
        {
            var config = _configStore.Load();
            var detected = _detector.Detect(config);

            Browsers.Clear();
            foreach (var browser in detected)
            {
                Browsers.Add(browser);
            }

            StatusMessage = $"已刷新，共 {Browsers.Count} 项";
        }
        catch (Exception ex)
        {
            StatusMessage = $"刷新失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 打开系统默认应用设置页，并提示用户选择对应浏览器。
    /// </summary>
    /// <param name="browser">目标浏览器。</param>
    public void SetAsDefault(BrowserInfo browser)
    {
        ArgumentNullException.ThrowIfNull(browser);

        try
        {
            _settingsLauncher.OpenDefaultApps();
            StatusMessage = $"请在打开的设置中选择：{browser.DisplayName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"无法打开系统设置：{ex.Message}";
        }
    }

    /// <summary>
    /// 将手动路径写入配置并刷新列表。
    /// </summary>
    /// <param name="browser">目标浏览器。</param>
    /// <param name="path">可执行文件路径。</param>
    public void AssignPath(BrowserInfo browser, string path)
    {
        ArgumentNullException.ThrowIfNull(browser);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("路径不能为空。", nameof(path));
        }

        try
        {
            var config = _configStore.Load();
            config.ManualPaths[browser.DisplayName] = path;
            _configStore.Save(config);
            Refresh();
            StatusMessage = $"已为 {browser.DisplayName} 指定路径";
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存路径失败：{ex.Message}";
        }
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OpenSettings()
    {
        LoadSettings();
        IsSettingsVisible = true;
    }

    private void LoadSettings()
    {
        var config = _configStore.Load();

        ManualPathEntries.Clear();
        foreach (var pair in config.ManualPaths
                     .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            ManualPathEntries.Add(new ManualPathEntry
            {
                DisplayName = pair.Key,
                Path = pair.Value
            });
        }

        _suppressStartupApply = true;
        try
        {
            StartWithWindows = config.StartWithWindows;
        }
        finally
        {
            _suppressStartupApply = false;
        }
    }

    private void SaveManualPaths()
    {
        try
        {
            var config = _configStore.Load();
            config.ManualPaths = ManualPathEntries
                .Where(e => !string.IsNullOrWhiteSpace(e.DisplayName))
                .GroupBy(
                    e => e.DisplayName.Trim(),
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.Last().Path?.Trim() ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase);

            // 去掉空路径条目
            var emptyKeys = config.ManualPaths
                .Where(p => string.IsNullOrWhiteSpace(p.Value))
                .Select(p => p.Key)
                .ToList();
            foreach (var key in emptyKeys)
            {
                config.ManualPaths.Remove(key);
            }

            config.StartWithWindows = StartWithWindows;
            _configStore.Save(config);
            Refresh();
            LoadSettings();
            StatusMessage = "手动路径已保存";
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存手动路径失败：{ex.Message}";
        }
    }

    private void RemoveManualPath(ManualPathEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ManualPathEntries.Remove(entry);
    }

    private void ApplyStartWithWindows(bool enabled)
    {
        try
        {
            var exePath = StartupRegistration.GetCurrentExecutablePath();
            _startupRegistration.Apply(enabled, exePath);

            var config = _configStore.Load();
            config.StartWithWindows = enabled;
            _configStore.Save(config);

            StatusMessage = enabled
                ? "已开启开机启动"
                : "已关闭开机启动";
        }
        catch (Exception ex)
        {
            _suppressStartupApply = true;
            try
            {
                // 回滚勾选状态，避免 UI 与注册表不一致
                SetField(ref _startWithWindows, !enabled);
            }
            finally
            {
                _suppressStartupApply = false;
            }

            StatusMessage = $"更新开机启动失败：{ex.Message}";
        }
    }

    private static string ResolveAppVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version?.ToString() ?? "0.0.0.0";
    }

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
