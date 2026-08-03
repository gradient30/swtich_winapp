using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
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
    private readonly DefaultBrowserResolver _defaultBrowserResolver;
    private readonly DefaultBrowserSwitcher _browserSwitcher;
    private string _statusMessage = string.Empty;
    private string _currentDefaultSummary = "当前默认浏览器：检测中…";
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
            new StartupRegistration(),
            new DefaultBrowserResolver(),
            null)
    {
    }

    /// <summary>
    /// 注入依赖，便于测试。
    /// </summary>
    public MainViewModel(
        ConfigStore configStore,
        BrowserDetector detector,
        SettingsLauncher settingsLauncher,
        StartupRegistration startupRegistration,
        DefaultBrowserResolver? defaultBrowserResolver = null,
        DefaultBrowserSwitcher? browserSwitcher = null)
    {
        _configStore = configStore
            ?? throw new ArgumentNullException(nameof(configStore));
        _detector = detector
            ?? throw new ArgumentNullException(nameof(detector));
        _settingsLauncher = settingsLauncher
            ?? throw new ArgumentNullException(nameof(settingsLauncher));
        _startupRegistration = startupRegistration
            ?? throw new ArgumentNullException(nameof(startupRegistration));
        _defaultBrowserResolver = defaultBrowserResolver
                                  ?? new DefaultBrowserResolver();
        _browserSwitcher = browserSwitcher
                           ?? new DefaultBrowserSwitcher(
                               _settingsLauncher,
                               _defaultBrowserResolver);

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

    /// <summary>当前系统默认浏览器摘要，用于主窗口顶部与托盘提示。</summary>
    public string CurrentDefaultSummary
    {
        get => _currentDefaultSummary;
        private set => SetField(ref _currentDefaultSummary, value);
    }

    /// <summary>当前默认浏览器显示名；无法识别时为 null。</summary>
    public string? CurrentDefaultDisplayName { get; private set; }

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
            var defaultId = _defaultBrowserResolver.ResolveCurrentDefaultId(
                detected);

            Browsers.Clear();
            BrowserInfo? currentDefault = null;
            foreach (var browser in detected)
            {
                var marked = new BrowserInfo
                {
                    Id = browser.Id,
                    DisplayName = browser.DisplayName,
                    ExecutablePath = browser.ExecutablePath,
                    Status = browser.Status,
                    IsCurrentDefault = browser.Id == defaultId
                };
                Browsers.Add(marked);
                if (marked.IsCurrentDefault)
                {
                    currentDefault = marked;
                }
            }

            CurrentDefaultDisplayName = currentDefault?.DisplayName;
            CurrentDefaultSummary = currentDefault is null
                ? "当前默认浏览器：未识别（可能是其他浏览器）"
                : $"当前默认浏览器：{currentDefault.DisplayName}";

            if (_configStore.LastLoadUsedFallback)
            {
                var reason = _configStore.LastLoadFallbackReason
                             ?? "配置已回退为默认";
                StatusMessage = reason;
            }
            else
            {
                StatusMessage = $"已刷新，共 {Browsers.Count} 项";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"刷新失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 尝试将指定浏览器设为默认（静默优先，失败回退系统设置）。
    /// </summary>
    /// <param name="browser">目标浏览器。</param>
    public void SetAsDefault(BrowserInfo browser)
    {
        ArgumentNullException.ThrowIfNull(browser);

        try
        {
            var result = _browserSwitcher.Switch(browser, Browsers.ToList());
            StatusMessage = result.Message;
            if (result.IsSilentSuccess
                || result.Outcome == BrowserSwitchOutcome.AlreadyDefault)
            {
                Refresh();
            }
            else if (result.Outcome == BrowserSwitchOutcome.FallbackToSettings)
            {
                // 设置页已由 Switcher 打开；稍后再刷新以反映用户操作
                Refresh();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"切换失败：{ex.Message}";
            try
            {
                _settingsLauncher.OpenDefaultApps();
                StatusMessage =
                    $"切换失败，已打开系统设置。请手动选择：{browser.DisplayName}";
            }
            catch (Exception openEx)
            {
                StatusMessage = $"无法打开系统设置：{openEx.Message}";
                MessageBox.Show(
                    $"无法打开系统默认应用设置页。\n\n{openEx.Message}\n\n"
                    + "请手动打开：设置 → 应用 → 默认应用。",
                    "浏览器切换器",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }

    /// <summary>
    /// 托盘快捷切换：非当前浏览器时尝试直切。
    /// </summary>
    /// <param name="browser">目标浏览器。</param>
    /// <returns>面向用户的结果说明。</returns>
    public string SwitchFromTray(BrowserInfo browser)
    {
        SetAsDefault(browser);
        return StatusMessage;
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
        var exePath = StartupRegistration.GetCurrentExecutablePath();
        var registryApplied = false;

        try
        {
            _startupRegistration.Apply(enabled, exePath);
            registryApplied = true;

            var config = _configStore.Load();
            config.StartWithWindows = enabled;
            _configStore.Save(config);

            StatusMessage = enabled
                ? "已开启开机启动"
                : "已关闭开机启动";
        }
        catch (Exception ex)
        {
            if (registryApplied)
            {
                try
                {
                    // 配置保存失败时撤销已写入的 Run 项
                    _startupRegistration.Apply(!enabled, exePath);
                }
                catch (Exception)
                {
                    // 尽力回滚；最终以状态提示为准
                }
            }

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
