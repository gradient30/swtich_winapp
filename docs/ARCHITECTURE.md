# 架构与核心原理

> 与代码同步至 **1.4.0**。实现位于 `src/BrowserSwitcher/`。

## 技术栈

| 层 | 选型 |
| --- | --- |
| 运行时 | .NET 8（`net8.0-windows`） |
| UI | WPF + MVVM（`MainViewModel` / `RelayCommand`） |
| 托盘 | Hardcodet.NotifyIcon.Wpf |
| 配置 | System.Text.Json → `%AppData%\BrowserSwitcher\config.json` |
| 测试 | xUnit（`tests/BrowserSwitcher.Tests`） |
| 发布 | 自包含单文件 exe（`win-x64`） |

## 目录与模块

```
src/BrowserSwitcher/
├── App.xaml.cs              # 启动、单实例、托盘生命周期
├── MainWindow.xaml          # 浏览器列表、状态、操作按钮
├── Views/SettingsView.xaml  # 手动路径、开机启动、直切开关、UCPD
├── ViewModels/
│   └── MainViewModel.cs     # 列表刷新、切换、设置读写
├── Models/
│   ├── BrowserInfo.cs       # Id / 显示名 / 路径 / 状态
│   └── AppConfig.cs         # ManualPaths, Theme, ForceDirectSwitch, …
├── Helpers/
│   ├── SingleInstance.cs    # 互斥单实例
│   └── TrayContextMenuPresenter.cs
└── Services/
    ├── BrowserDetector.cs           # 注册表 + 常见路径扫描
    ├── DefaultBrowserResolver.cs    # 读 http UserChoice → 浏览器 Id
    ├── DefaultBrowserSwitcher.cs    # 切换编排（双模式）
    ├── UserChoiceAssociationWriter.cs  # UserChoice ProgId + Hash 直写
    ├── UserChoiceHash.cs            # Hash 算法（Firefox 同源）
    ├── ExternalAssociationTools.cs  # SetUserFTA / SetDefaultBrowser 定位与参数
    ├── AdminProcessLauncher.cs      # UAC 提权运行外部 exe / cmd
    ├── UcpdManager.cs               # UCPD 状态检测与禁用
    ├── SettingsLauncher.cs          # ms-settings:defaultapps
    ├── ConfigStore.cs
    ├── ThemeService.cs
    ├── StartupRegistration.cs
    └── ReleaseNotesService.cs
```

## 数据流

```
启动
  → ConfigStore.Load()
  → BrowserDetector 扫描 + 合并 ManualPaths
  → DefaultBrowserResolver 读当前默认（http ProgId）
  → 填充 BrowserInfo 列表 → 主窗口 & 托盘菜单

用户点击「设为默认 / 托盘项」
  → MainViewModel.SwitchTo(browser)
  → DefaultBrowserSwitcher.Switch(..., forceDirectSwitch)
  → 按模式走直切链或合规回退链
  → BrowserSwitchResult → 状态栏 / MessageBox
  → 刷新列表（当前默认标记）
```

## Windows 默认浏览器：系统机制

### 判定依据

系统与用户界面认定的「默认 Web 浏览器」主要来自当前用户注册表：

```
HKCU\Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice
  ProgId  = ChromeHTML | BraveHTML | MSEdgeHTM | IE.HTTP | …
  Hash    = Base64 校验串
```

本应用通过 `DefaultBrowserResolver` **只读** `http\UserChoice\ProgId`，再映射到已知浏览器 Id（`chrome` / `brave` / `edge` / `ie`）。必要时回退解析 ProgId 的 `open\command` 可执行路径。

### UserChoice 保护

自 Windows 8 起，微软要求用户经系统 UI 确认默认应用；直接改 ProgId 而不带合法 Hash 会被忽略或还原。

自 **2024 年前后**，内核驱动 **UCPD**（User Choice Protection Driver）进一步拦截第三方对 UserChoice 的写入。仅「以管理员运行」不足以绕过 UCPD；常见对策：

| 手段 | 说明 |
| --- | --- |
| 禁用 UCPD 并重启 | `sc config UCPD start= disabled`（本应用设置页可提权执行） |
| SetUserFTA 2.x | 第三方工具，利用微软仍接受的写入路径（需用户自行下载） |
| 内置 Hash 直写 | 在 UCPD **未加载**时，写入 ProgId + 正确 Hash（本应用 1.4.0） |
| 系统设置 UI | `ms-settings:defaultapps`、关联 UI（合规，需用户点确认） |

`.htm` / `.html` 的 FileExts\UserChoice 在多数系统上 ACL 更严，本应用直写时**仅强制成功 http + https**；`.htm` / `.html` 尽力写入，失败不影响 URL 默认浏览器判定。

### UserChoice Hash 算法

`UserChoiceHash` 移植自 Firefox 实现，输入字符串形如（小写）：

```
{extension}{UserSid}{ProgId}{FileTimeHighHex}{FileTimeLowHex}User Choice set via Windows User Experience {D18B6DD5-6124-4341-9318-804003BAFA0B}
```

- `extension`：`http`、`https`、`.htm`、`.html`
- `FileTime`：UserChoice 注册表键**写入前**的最后修改时间（`RegQueryInfoKey` 读取；Win10 无 `RegGetKeyLastWriteTime` API）
- 对 Unicode 字符串 + `\0` 做 MD5，再经自定义混合运算，输出 8 字节 Base64 作为 `Hash` 值

写入顺序：读时间戳 → 写 `ProgId` → 计算 Hash → 写 `Hash`。

### ProgId 映射

| 浏览器 Id | ProgId |
| --- | --- |
| chrome | ChromeHTML |
| brave | BraveHTML |
| edge | MSEdgeHTM |
| ie | IE.HTTP |

## 切换编排：双模式

`AppConfig.ForceDirectSwitch` 默认 `true`。由 `DefaultBrowserSwitcher.Switch(..., forceDirectSwitch)` 分支。

### 模式 A — 一键直切（ForceDirectSwitch = true）

```
已是默认? → AlreadyDefault
未安装?   → 打开设置页 + 提示

TryForceDirectSwitch:
  0. UserChoiceAssociationWriter.TrySetDefaultBrowserById
     → 写 http/https ProgId+Hash → ResolveCurrentDefaultId 验证
  1. AdminProcessLauncher + SetUserFTA.exe（Tools\ 或程序目录）
  2. AdminProcessLauncher + SetDefaultBrowser.exe
  3. COM SetAppAsDefault
  任一成功且解析到目标 Id → Switched

全部失败 → ForceSwitchFailed（UCPD 状态、工具缺失等提示，不打开设置页）
```

### 模式 B — 合规回退（ForceDirectSwitch = false）

```
  1. 非提权 SetDefaultBrowser.exe（若存在）→ 验证
  2. COM SetAppAsDefault → 验证
  3. LaunchAdvancedAssociationUI
  4. ms-settings:defaultapps?registeredAppMachine|User=…
  5. ms-settings:defaultapps（总页）
  → FallbackToSettings + 用户可见提示
```

### 外部工具约定

`ExternalAssociationTools` 在以下位置查找 exe（不随仓库分发）：

- `{AppDir}\SetUserFTA.exe` 或 `{AppDir}\Tools\SetUserFTA.exe`
- `{AppDir}\SetDefaultBrowser.exe` 或 `{AppDir}\Tools\SetDefaultBrowser.exe`

直切模式下通过 `AdminProcessLauncher.TryRun(exe, args)` 以 `runas` 提权；标准模式下 SetDefaultBrowser 以普通进程、5 秒超时等待。

## 浏览器检测

`BrowserDetector` 对 `chrome` / `brave` / `edge` / `ie` 依次：

1. 注册表（App Paths、卸载信息、RegisteredApplications 等）
2. 常见安装路径（Program Files、LocalAppData）
3. `config.json` 中的 `ManualPaths` 覆盖自动结果

单项失败不影响其他浏览器。路径失效时在 UI 标记「未找到」。

## 配置项（AppConfig）

| 字段 | 默认 | 含义 |
| --- | --- | --- |
| ManualPaths | `{}` | 浏览器 Id → 可执行文件路径 |
| StartWithWindows | false | 当前用户 Run 注册表开机启动 |
| Theme | Dark | Dark / Light |
| ForceDirectSwitch | **true** | 托盘一键直切 vs 合规设置页回退 |

## 测试策略

- **单元测试**：ConfigStore、Hash 字符串、ExternalAssociationTools 参数、Resolver 映射、ViewModel 命令等（`dotnet test` 默认运行）
- **实机集成**：`LiveSwitchIntegrationTests` 在本机往返 Chrome↔Brave；默认跳过，设置环境变量 `RUN_LIVE_SWITCH_TESTS=1` 后运行

## 已知限制

- Win11 24H2+ 可能引入 `UserChoiceLatest` 等新机制，旧 Hash 方案可能失效，需在新系统上验证
- 面向**个人本地**直切；分发场景应关闭 `ForceDirectSwitch` 并依赖系统设置 UI
- 无热键、无除四款以外的自动发现（可后续扩展）
