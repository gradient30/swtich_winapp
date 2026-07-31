# Browser Switcher Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 实现一个 .NET 8 WPF 托盘小工具，自动检测 Brave/Chrome/IE/Edge，一键打开系统默认应用设置页并提示用户选择目标浏览器。

**Architecture:** 单实例 WPF 应用 + Hardcodet 托盘图标；BrowserDetector 扫描注册表/常见路径并与 ConfigStore 手动路径合并；SettingsLauncher 仅打开 `ms-settings:defaultapps`，不修改系统关联。

**Tech Stack:** .NET 8、WPF、Hardcodet.NotifyIcon.Wpf、System.Text.Json、xUnit

**Design Doc:** `docs/plans/2026-07-31-browser-switcher-design.md`

---

### Task 1: 创建解决方案与测试项目

**Files:**
- Create: `BrowserSwitcher.sln`
- Create: `src/BrowserSwitcher/BrowserSwitcher.csproj`
- Create: `tests/BrowserSwitcher.Tests/BrowserSwitcher.Tests.csproj`

**Step 1: 创建解决方案结构**

```powershell
dotnet new sln -n BrowserSwitcher
dotnet new wpf -n BrowserSwitcher -o src/BrowserSwitcher -f net8.0
dotnet new xunit -n BrowserSwitcher.Tests -o tests/BrowserSwitcher.Tests -f net8.0
dotnet sln BrowserSwitcher.sln add src/BrowserSwitcher/BrowserSwitcher.csproj
dotnet sln BrowserSwitcher.sln add tests/BrowserSwitcher.Tests/BrowserSwitcher.Tests.csproj
dotnet add tests/BrowserSwitcher.Tests/BrowserSwitcher.Tests.csproj reference src/BrowserSwitcher/BrowserSwitcher.csproj
```

**Step 2: 添加托盘 NuGet 包**

```powershell
dotnet add src/BrowserSwitcher/BrowserSwitcher.csproj package Hardcodet.NotifyIcon.Wpf
```

**Step 3: 验证可编译**

Run: `dotnet build BrowserSwitcher.sln`
Expected: Build succeeded

**Step 4: Commit**

```powershell
git add BrowserSwitcher.sln src/BrowserSwitcher tests/BrowserSwitcher.Tests
git commit -m "chore: scaffold .NET 8 WPF solution and xUnit project"
```

---

### Task 2: BrowserInfo 模型与 ConfigStore

**Files:**
- Create: `src/BrowserSwitcher/Models/BrowserInfo.cs`
- Create: `src/BrowserSwitcher/Models/AppConfig.cs`
- Create: `src/BrowserSwitcher/Services/ConfigStore.cs`
- Create: `tests/BrowserSwitcher.Tests/ConfigStoreTests.cs`

**Step 1: 写失败测试（配置读写与损坏回退）**

```csharp
using BrowserSwitcher.Models;
using BrowserSwitcher.Services;

namespace BrowserSwitcher.Tests;

public class ConfigStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsManualPaths()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bs-test-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            var store = new ConfigStore(dir);
            var config = new AppConfig
            {
                ManualPaths = new Dictionary<string, string>
                {
                    ["Google Chrome"] = @"C:\Program Files\Google\Chrome\Application\chrome.exe"
                }
            };
            store.Save(config);
            var loaded = store.Load();
            Assert.Equal(
                config.ManualPaths["Google Chrome"],
                loaded.ManualPaths["Google Chrome"]);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaultConfig()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bs-test-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "config.json"), "{not-json");
            var store = new ConfigStore(dir);
            var loaded = store.Load();
            Assert.NotNull(loaded.ManualPaths);
            Assert.Empty(loaded.ManualPaths);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
```

**Step 2: 运行测试确认失败**

Run: `dotnet test tests/BrowserSwitcher.Tests --filter ConfigStoreTests`
Expected: FAIL（类型不存在）

**Step 3: 实现模型与 ConfigStore**

`BrowserInfo.cs`:

```csharp
namespace BrowserSwitcher.Models;

public enum BrowserStatus
{
    Installed,
    Manual,
    NotFound
}

public sealed class BrowserInfo
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public string? ExecutablePath { get; init; }
    public BrowserStatus Status { get; init; }
}
```

`AppConfig.cs`:

```csharp
namespace BrowserSwitcher.Models;

public sealed class AppConfig
{
    public Dictionary<string, string> ManualPaths { get; set; } = new();
    public bool StartWithWindows { get; set; }
}
```

`ConfigStore.cs`:

```csharp
using System.Text.Json;
using BrowserSwitcher.Models;

namespace BrowserSwitcher.Services;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _configPath;

    public ConfigStore(string? directory = null)
    {
        var dir = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BrowserSwitcher");
        Directory.CreateDirectory(dir);
        _configPath = Path.Combine(dir, "config.json");
    }

    public AppConfig Load()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                return new AppConfig();
            }

            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions)
                   ?? new AppConfig();
        }
        catch (JsonException)
        {
            return new AppConfig();
        }
        catch (IOException)
        {
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(_configPath, json);
    }
}
```

说明：WPF 项目默认不作为类库被测试引用时，需在 `BrowserSwitcher.csproj` 中确保可被引用（SDK 风格 WPF 项目可直接被 xUnit 引用）。若 InternalsVisibleTo 需要，本任务公开 API 即可。

**Step 4: 运行测试确认通过**

Run: `dotnet test tests/BrowserSwitcher.Tests --filter ConfigStoreTests`
Expected: PASS

**Step 5: Commit**

```powershell
git add src/BrowserSwitcher/Models src/BrowserSwitcher/Services/ConfigStore.cs tests/BrowserSwitcher.Tests/ConfigStoreTests.cs
git commit -m "feat: add AppConfig and ConfigStore with corrupt-file fallback"
```

---

### Task 3: BrowserDetector

**Files:**
- Create: `src/BrowserSwitcher/Services/BrowserDetector.cs`
- Create: `tests/BrowserSwitcher.Tests/BrowserDetectorTests.cs`

**Step 1: 写失败测试（合并手动路径、未找到标记）**

```csharp
using BrowserSwitcher.Models;
using BrowserSwitcher.Services;

namespace BrowserSwitcher.Tests;

public class BrowserDetectorTests
{
    [Fact]
    public void Detect_ManualPathExists_OverridesAsManual()
    {
        var tempExe = Path.Combine(Path.GetTempPath(), $"chrome-{Guid.NewGuid()}.exe");
        File.WriteAllBytes(tempExe, Array.Empty<byte>());
        try
        {
            var config = new AppConfig
            {
                ManualPaths = { ["Google Chrome"] = tempExe }
            };
            var detector = new BrowserDetector();
            var list = detector.Detect(config);
            var chrome = Assert.Single(list, b => b.Id == "chrome");
            Assert.Equal(BrowserStatus.Manual, chrome.Status);
            Assert.Equal(tempExe, chrome.ExecutablePath);
        }
        finally
        {
            File.Delete(tempExe);
        }
    }

    [Fact]
    public void Detect_AlwaysReturnsFourKnownBrowsers()
    {
        var detector = new BrowserDetector();
        var list = detector.Detect(new AppConfig());
        Assert.Equal(4, list.Count);
        Assert.Contains(list, b => b.Id == "brave");
        Assert.Contains(list, b => b.Id == "chrome");
        Assert.Contains(list, b => b.Id == "ie");
        Assert.Contains(list, b => b.Id == "edge");
    }
}
```

**Step 2: 运行测试确认失败**

Run: `dotnet test tests/BrowserSwitcher.Tests --filter BrowserDetectorTests`
Expected: FAIL

**Step 3: 实现 BrowserDetector**

探测逻辑要点：
- 内置四项：`brave` / `chrome` / `ie` / `edge`，DisplayName 分别为 Brave、Google Chrome、Internet Explorer、Microsoft Edge
- 对每项：若 `ManualPaths` 有键且文件存在 → `Manual`；否则查注册表 `App Paths` 与常见路径；找到 → `Installed`；否则 `NotFound`（ExecutablePath 为 null）
- IE：优先 `iexplore.exe` App Paths；Edge：`msedge.exe`；Chrome：`chrome.exe`；Brave：`brave.exe`
- 使用 `Microsoft.Win32.Registry` 读 `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\<exe>\` 默认值，并兼顾 `RegistryView.Registry64` / `Registry32`

**Step 4: 运行测试确认通过**

Run: `dotnet test tests/BrowserSwitcher.Tests --filter BrowserDetectorTests`
Expected: PASS

**Step 5: Commit**

```powershell
git add src/BrowserSwitcher/Services/BrowserDetector.cs tests/BrowserSwitcher.Tests/BrowserDetectorTests.cs
git commit -m "feat: detect Brave/Chrome/IE/Edge with manual path override"
```

---

### Task 4: SettingsLauncher

**Files:**
- Create: `src/BrowserSwitcher/Services/SettingsLauncher.cs`
- Create: `tests/BrowserSwitcher.Tests/SettingsLauncherTests.cs`

**Step 1: 写失败测试（构造 ProcessStartInfo）**

为可测性，让 `SettingsLauncher` 暴露构建 URI 的方法，或接受可替换的启动委托：

```csharp
using BrowserSwitcher.Services;

namespace BrowserSwitcher.Tests;

public class SettingsLauncherTests
{
    [Fact]
    public void CreateStartInfo_UsesDefaultAppsUri()
    {
        var info = SettingsLauncher.CreateStartInfo();
        Assert.Equal("ms-settings:defaultapps", info.FileName);
        Assert.Equal(System.Diagnostics.ProcessWindowStyle.Normal, info.WindowStyle);
        Assert.True(info.UseShellExecute);
    }
}
```

**Step 2: 运行测试确认失败**

Run: `dotnet test tests/BrowserSwitcher.Tests --filter SettingsLauncherTests`
Expected: FAIL

**Step 3: 实现 SettingsLauncher**

```csharp
using System.Diagnostics;

namespace BrowserSwitcher.Services;

public sealed class SettingsLauncher
{
    public static ProcessStartInfo CreateStartInfo()
    {
        return new ProcessStartInfo
        {
            FileName = "ms-settings:defaultapps",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Normal
        };
    }

    public void OpenDefaultApps()
    {
        Process.Start(CreateStartInfo());
    }
}
```

**Step 4: 运行测试确认通过**

Run: `dotnet test tests/BrowserSwitcher.Tests --filter SettingsLauncherTests`
Expected: PASS

**Step 5: Commit**

```powershell
git add src/BrowserSwitcher/Services/SettingsLauncher.cs tests/BrowserSwitcher.Tests/SettingsLauncherTests.cs
git commit -m "feat: open ms-settings default apps page"
```

---

### Task 5: 主窗口 UI 与 ViewModel

**Files:**
- Create: `src/BrowserSwitcher/ViewModels/MainViewModel.cs`
- Modify: `src/BrowserSwitcher/MainWindow.xaml`
- Modify: `src/BrowserSwitcher/MainWindow.xaml.cs`
- Create: `src/BrowserSwitcher/Views/SettingsView.xaml`（可先简单 Frame/ContentControl 切换）
- Create: `src/BrowserSwitcher/Views/SettingsView.xaml.cs`

**Step 1: 实现 MainViewModel**

职责：
- `ObservableCollection<BrowserInfo> Browsers`
- `Refresh()`：Load config → Detect → 更新集合
- `SetAsDefault(BrowserInfo)`：调用 SettingsLauncher + 设置 `StatusMessage = $"请在打开的设置中选择：{name}"`
- `AssignPath(BrowserInfo, string path)`：写入 ManualPaths（键用 DisplayName）→ Save → Refresh
- 捕获打开设置失败，设置错误消息

**Step 2: 实现 MainWindow.xaml**

布局：
- ListBox/ItemsControl 绑定 Browsers（显示名称、状态、路径）
- 每项按钮：「设为默认」「指定路径」（OpenFileDialog filter `*.exe`）
- 底部：刷新、设置、状态文本
- `Closing` 事件：`e.Cancel = true; Hide();`（由 App 在真正退出时关闭）

**Step 3: 编译运行冒烟**

Run: `dotnet build src/BrowserSwitcher`
Expected: Build succeeded  
手动：`dotnet run --project src/BrowserSwitcher` 确认窗口列表与按钮可用（托盘下个任务再接）

**Step 4: Commit**

```powershell
git add src/BrowserSwitcher/ViewModels src/BrowserSwitcher/MainWindow.xaml src/BrowserSwitcher/MainWindow.xaml.cs src/BrowserSwitcher/Views
git commit -m "feat: add main window browser list and set-default actions"
```

---

### Task 6: 托盘、单实例与应用生命周期

**Files:**
- Modify: `src/BrowserSwitcher/App.xaml`
- Modify: `src/BrowserSwitcher/App.xaml.cs`
- Create: `src/BrowserSwitcher/Resources/tray.ico`（可用系统图标或简单嵌入图标）
- 可选：`src/BrowserSwitcher/Helpers/SingleInstance.cs`

**Step 1: 单实例**

使用命名 Mutex（如 `Global\BrowserSwitcher_SingleInstance`）：
- 若已存在：通过简单 IPC（Named Pipe 或 `EventWaitHandle`）通知已有实例显示主窗口，然后退出
- 首版可用 Mutex + `EventWaitHandle`：「ShowWindow」事件，主实例后台线程等待后 `Dispatcher.Invoke` 显示窗口

**Step 2: 托盘**

在 `App.xaml` 资源中放 `TaskbarIcon`：
- TrayToolTipText / ToolTip：`浏览器切换器`
- 左键：切换 MainWindow 显隐
- ContextMenu：动态根据当前浏览器列表生成菜单项；「打开主窗口」「退出」
- 退出：释放 Mutex、`taskbarIcon.Dispose()`、`Shutdown()`

**Step 3: StartupUri**

去掉自动打开，或打开后立即 Hide；默认仅托盘可见。

**Step 4: 手动验收**

1. 启动 → 仅托盘
2. 左键 → 主窗口；关闭 → 回到托盘
3. 右键选浏览器 → 打开设置页 + 提示
4. 再运行一次 exe → 不出现第二图标，激活已有窗口

**Step 5: Commit**

```powershell
git add src/BrowserSwitcher/App.xaml src/BrowserSwitcher/App.xaml.cs src/BrowserSwitcher/Helpers src/BrowserSwitcher/Resources
git commit -m "feat: add tray icon, single-instance, and hide-to-tray"
```

---

### Task 7: 设置页与 README

**Files:**
- Modify: `src/BrowserSwitcher/Views/SettingsView.xaml`
- Modify: `src/BrowserSwitcher/Views/SettingsView.xaml.cs`
- Create: `README.md`

**Step 1: 设置页最小功能**
- 列出/编辑 ManualPaths
- 「开机启动」写入当前用户 Run 键（或先 UI 绑定 `AppConfig.StartWithWindows` 并实现）
- 显示版本号（程序集版本）

**Step 2: README**
- 如何 `dotnet run` / 发布：`dotnet publish -c Release -r win-x64 --self-contained false`
- 使用说明：托盘操作、手动路径、系统设置页确认

**Step 3: 全量测试**

Run: `dotnet test BrowserSwitcher.sln`
Expected: 全部 PASS

**Step 4: Commit**

```powershell
git add src/BrowserSwitcher/Views README.md
git commit -m "feat: add settings view and project README"
```

---

## 执行备注

- 严格按 Task 顺序；每 Task 内遵循 TDD（有测试的任务）
- 不要实现静默改默认浏览器
- IE 在较新 Windows 上可能仅残留 `iexplore.exe` 重定向到 Edge，检测为「找到路径」即可，提示文案仍用 Internet Explorer
- 图标资源若暂缺，可用 `pack://` 嵌入或临时使用 `SystemIcons.Application` 转 BitmapSource
