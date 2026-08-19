# 开机启动实现方案（Browser Switcher）

> 状态：**已在生产使用（1.4.x，100% 可用）**  
> 适用：Windows 10/11 桌面托盘类 .NET 应用  
> 实现位置：`src/BrowserSwitcher/Services/StartupRegistration.cs`

本文档供其它研发人员**参考并落地**同类能力，描述本仓库已验证的开机启动方案，包括原理、模块划分、交互时序、异常处理与测试方法。

---

## 1. 目标与约束

### 1.1 产品目标

- 用户可在「设置 → 启动」勾选 **「开机时自动启动」**
- 开启后，用户登录 Windows 时应用自动运行，**托盘常驻、主窗口默认隐藏**
- 关闭后，不再随系统启动
- **不需要管理员权限**（仅写当前用户注册表）

### 1.2 非目标（本方案不做）

| 项 | 说明 |
| --- | --- |
| 任务计划程序 | 未使用 `schtasks` / Task Scheduler |
| 启动文件夹快捷方式 | 未使用 `%AppData%\Microsoft\Windows\Start Menu\Programs\Startup` |
| Windows 服务 | 未注册 Service |
| 机器级（所有用户）启动 | 仅 HKCU，不影响其它账户 |
| 开机自启参数 | Run 项仅启动 exe，无 `--minimized` 等命令行 |

---

## 2. 技术选型

### 2.1 方案：当前用户 Run 注册表

Windows 登录后会执行：

```
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
```

本应用在开启时写入一条字符串值，值为**带引号的 exe 完整路径**：

| 字段 | 值 |
| --- | --- |
| 根键 | `HKEY_CURRENT_USER` |
| 子键 | `Software\Microsoft\Windows\CurrentVersion\Run` |
| 值名称 | `BrowserSwitcher`（常量 `StartupRegistration.DefaultValueName`） |
| 值数据 | `"D:\path\to\BrowserSwitcher.exe"` |

关闭时 **DeleteValue**，而非写空字符串。

### 2.2 为何选 Run 而非其它

| 方案 | 优点 | 本项目的取舍 |
| --- | --- | --- |
| **HKCU Run** | 实现简单、无 UAC、用户可勾选控制 | **已采用** |
| Startup 文件夹 | 用户可见、易排查 | 需处理快捷方式、路径变更；未采用 |
| 任务计划 | 可延迟启动、条件丰富 | 复杂度高、过度设计；未采用 |
| 服务 | 系统级 | 需管理员、不适合托盘 UI 工具；未采用 |

---

## 3. 架构与模块

```
┌─────────────────────────────────────────────────────────────┐
│  SettingsView.xaml                                          │
│  CheckBox IsChecked="{Binding StartWithWindows}"            │
└───────────────────────────┬─────────────────────────────────┘
                            │ 绑定（草稿态）
┌───────────────────────────▼─────────────────────────────────┐
│  MainViewModel                                              │
│  - StartWithWindows 属性（草稿 / 即时生效分支）              │
│  - SaveManualPaths() → ApplyStartWithWindows + 写 config    │
│  - CancelSettings() → 回滚 _startupSnapshot                 │
└───────────────┬─────────────────────────┬───────────────────┘
                │                         │
    ┌───────────▼──────────┐   ┌──────────▼─────────┐
    │ StartupRegistration  │   │ ConfigStore          │
    │ Apply / IsRegistered │   │ config.json          │
    └───────────┬──────────┘   └──────────────────────┘
                │
    ┌───────────▼──────────────────────────────────────┐
    │ HKCU\...\Run\BrowserSwitcher = "...\exe"         │
    └──────────────────────────────────────────────────┘

开机登录 → Windows 读取 Run → 启动 BrowserSwitcher.exe
         → App.OnStartup → SingleInstance → 托盘常驻
```

### 3.1 核心类职责

| 模块 | 文件 | 职责 |
| --- | --- | --- |
| 注册表写入 | `Services/StartupRegistration.cs` | 读写 Run 键；路径加引号；可注入测试用注册表 |
| 配置持久化 | `Services/ConfigStore.cs` | `%AppData%\BrowserSwitcher\config.json` |
| 配置模型 | `Models/AppConfig.cs` | `StartWithWindows` 布尔字段 |
| UI 绑定 | `Views/SettingsView.xaml` | 「开机时自动启动」复选框 |
| 业务编排 | `ViewModels/MainViewModel.cs` | 草稿/保存/取消/失败回滚 |
| 启动入口 | `App.xaml.cs` | 单实例 + 托盘；**不**额外处理开机逻辑 |
| 单实例 | `Helpers/SingleInstance.cs` | 防止 Run 重复启动多个托盘实例 |
| 单元测试 | `tests/.../StartupRegistrationTests.cs` | 临时注册表键往返测试 |

---

## 4. 核心 API（可直接复用）

### 4.1 StartupRegistration

```csharp
// 启用
registration.Apply(true, StartupRegistration.GetCurrentExecutablePath());

// 禁用
registration.Apply(false, exePath); // 禁用时路径可为任意非空，内部会 DeleteValue

// 查询是否已注册
bool on = registration.IsRegistered();
```

**路径来源：** 始终使用 `Environment.ProcessPath`（封装为 `GetCurrentExecutablePath()`），确保写入的是**当前正在运行的 exe**，适配单文件发布与目录部署。

**路径格式：** `QuotePath()` 保证带空格的路径合法：

```csharp
// 输入: C:\Program Files\App\app.exe
// 写入: "C:\Program Files\App\app.exe"
```

### 4.2 注册表读写细节

```csharp
public const string RunSubKeyPath =
    @"Software\Microsoft\Windows\CurrentVersion\Run";

public const string DefaultValueName = "BrowserSwitcher";
```

- **启用：** `OpenSubKey(..., writable: true)` → `SetValue(name, quotedPath)`
- **禁用：** `DeleteValue(name, throwOnMissingValue: false)`（键不存在不抛异常）
- **权限：** 仅 HKCU，普通用户可写

---

## 5. 配置双写：注册表 + JSON

开机是否生效，**以 Run 注册表为准**；`config.json` 用于 UI 回显与用户偏好持久化。

| 存储 | 路径 | 字段 | 作用 |
| --- | --- | --- | --- |
| 注册表 | `HKCU\...\Run` | `BrowserSwitcher` | **系统实际开机启动** |
| JSON | `%AppData%\BrowserSwitcher\config.json` | `StartWithWindows` | 设置页勾选状态、下次打开回显 |

示例 `config.json` 片段：

```json
{
  "ManualPaths": { },
  "StartWithWindows": true,
  "Theme": "Dark",
  "ForceDirectSwitch": true
}
```

> **注意：** 应用启动时**不会**主动扫描 Run 键同步 UI；打开设置页时从 `config.json` 加载勾选状态。若用户手工改注册表，需以注册表实际状态为准，或在产品中增加「启动时校验 Run 与 config 一致性」（当前未做）。

---

## 6. UI 交互时序（草稿保存模式）

本项目的开机启动采用 **「设置页草稿 + 保存提交」**，避免用户误触立即写注册表。

### 6.1 打开设置

```
OpenSettings()
  → LoadSettings()        // 从 config 读 StartWithWindows
  → _startupSnapshot = config.StartWithWindows
  → IsSettingsVisible = true
```

设置页内勾选 **不会** 立即写 Run（因 `IsSettingsVisible == true`）。

### 6.2 点击「保存」

```
SaveManualPaths()
  → ApplyStartWithWindows(StartWithWindows)   // 写 Run + 写 config
  → config.StartWithWindows = StartWithWindows
  → ConfigStore.Save(config)
  → _startupSnapshot = StartWithWindows       // 更新快照
```

### 6.3 点击「取消」或关闭

```
CancelSettings()
  → StartWithWindows = _startupSnapshot       // 丢弃草稿
  → IsSettingsVisible = false
```

### 6.4 设置页外的即时生效（保留能力）

若 `IsSettingsVisible == false` 且用户修改 `StartWithWindows`，会**立即**调用 `ApplyStartWithWindows`。当前 UI 仅在设置页提供勾选，此分支为 API 预留。

### 6.5 相关 ViewModel 标志位

| 字段 | 用途 |
| --- | --- |
| `_suppressStartupApply` | 加载/回滚勾选时抑制写注册表 |
| `_startupSnapshot` | 打开设置时的原始值，供取消恢复 |
| `IsSettingsVisible` | 设置页可见时不即时写 Run |

---

## 7. 失败处理与一致性

`ApplyStartWithWindows(bool enabled)` 流程：

1. `StartupRegistration.Apply(enabled, exePath)` 写注册表
2. `ConfigStore.Save` 写 JSON
3. 任一步失败：
   - 若注册表已写但 JSON 失败 → **回滚注册表**（`Apply(!enabled, ...)`）
   - UI 勾选回滚为原值
   - 状态栏提示：`更新开机启动失败：...`

成功提示：

- 开启：`已开启开机启动`
- 关闭：`已关闭开机启动`

---

## 8. 与应用启动流程的配合

`App.xaml.cs` **不包含**开机启动专用逻辑。Run 项触发的启动与用户双击 exe 走同一路径：

```
OnStartup
  → 加载主题 (ConfigStore)
  → SingleInstance.Create()
       ├─ 非主实例 → SignalShowWindow → Shutdown
       └─ 主实例 → MainWindow.Show() → Hide() → 托盘图标
```

### 8.1 单实例（必须）

开机 Run 若被重复触发（或用户手动再开），`SingleInstance` 保证只有一个托盘进程：

- Mutex：`Global\BrowserSwitcher_SingleInstance`
- 次要实例通过 Event 通知主实例显示窗口后退出

**落地建议：** 凡使用 Run 开机自启的托盘程序，应同时实现单实例，否则可能多托盘图标。

### 8.2 托盘默认隐藏

```csharp
_mainWindow.Show();
_mainWindow.Hide();
```

用户登录后仅见托盘，符合「后台工具」预期。

---

## 9. 在新项目中落地步骤

### Step 1：复制/实现 StartupRegistration

- 复制 `StartupRegistration.cs`
- 修改 `DefaultValueName` 为你的产品名（避免与其它应用冲突）
- 保持 `RunSubKeyPath` 不变（标准 Windows 位置）

### Step 2：配置模型

```csharp
public bool StartWithWindows { get; set; }
```

### Step 3：ViewModel 编排

参考 `MainViewModel` 中：

- `StartWithWindows` 属性（含 `IsSettingsVisible` / `_suppressStartupApply` 分支）
- `ApplyStartWithWindows`
- `LoadSettings` / `CancelSettings` / `SaveManualPaths`

### Step 4：UI

```xml
<CheckBox Content="开机时自动启动"
          IsChecked="{Binding StartWithWindows}" />
```

### Step 5：单实例 + 托盘

确保 `App` 启动逻辑支持「无窗口、仅托盘」。

### Step 6：测试

复制 `StartupRegistrationTests.cs`，使用临时 HKCU 子键，**不要**在测试中写真实 Run 键。

---

## 10. 测试与验收

### 10.1 自动化测试

```powershell
dotnet test --filter "FullyQualifiedName~StartupRegistration"
```

覆盖：

- 启用 → `IsRegistered() == true`，值含 exe 名
- 禁用 → `IsRegistered() == false`
- 空路径启用 → `ArgumentException`

### 10.2 手工验收清单

| # | 步骤 | 预期 |
| --- | --- | --- |
| 1 | 设置中勾选「开机时自动启动」→ 保存 | 状态栏提示已开启 |
| 2 | `reg query HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v BrowserSwitcher` | 存在且路径为当前 exe |
| 3 | 注销并重新登录（或重启） | 托盘自动出现，无主窗口 |
| 4 | 再次手动启动 exe | 仅一个托盘实例 |
| 5 | 取消勾选 → 保存 | Run 值被删除 |
| 6 | 重启 | 应用不再自启 |
| 7 | 打开设置 → 勾选 → 取消 | 重启后仍不自启（草稿未提交） |
| 8 | 移动 exe 到新目录后再次保存 | Run 路径更新为新位置 |

---

## 11. 已知限制与运维提示

| 场景 | 行为 |
| --- | --- |
| 用户移动/删除 exe | Run 仍指向旧路径，启动失败；需重新打开设置保存一次 |
| 组策略禁用 Run 启动项 | 写入可能成功但系统不执行；需 IT 策略排查 |
| 便携版（路径常变） | 每次换目录后应提示用户重新勾选保存 |
| 多用户 | 每用户独立 HKCU，互不影响 |
| 与安全软件 | 部分杀软会拦截 Run 写入或启动，属环境差异 |

---

## 12. 可选扩展（未实现，供参考）

1. **启动时校验一致性：** `IsRegistered()` 与 `config.StartWithWindows` 不一致时自动修复或提示  
2. **启动参数：** Run 值写 `"app.exe" --tray`，`App` 解析参数决定静默/显示窗口  
3. **Startup 快捷方式：** 面向非技术用户更易在资源管理器中看到  
4. **任务计划延迟启动：** 减轻登录瞬间 IO 压力（对本工具非必要）

---

## 13. 关键代码索引

| 内容 | 路径 |
| --- | --- |
| Run 注册表读写 | `src/BrowserSwitcher/Services/StartupRegistration.cs` |
| ViewModel 编排 | `src/BrowserSwitcher/ViewModels/MainViewModel.cs`（`ApplyStartWithWindows`） |
| 设置 UI | `src/BrowserSwitcher/Views/SettingsView.xaml` |
| 配置字段 | `src/BrowserSwitcher/Models/AppConfig.cs` |
| 应用入口 | `src/BrowserSwitcher/App.xaml.cs` |
| 单实例 | `src/BrowserSwitcher/Helpers/SingleInstance.cs` |
| 单元测试 | `tests/BrowserSwitcher.Tests/StartupRegistrationTests.cs` |

---

## 14. 总结

本项目的开机启动方案是 **HKCU Run + JSON 双写 + 设置页草稿保存**，配合 **单实例托盘启动**，已在 Browser Switcher 1.4.x 稳定运行。其它项目复用时，优先复制 `StartupRegistration` 与 ViewModel 时序逻辑，并务必保留单实例与失败回滚，即可达到同等可用性。
