# 托盘默认应用设置快捷跳转 Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 在托盘右键菜单增加「打开默认应用设置…」，一键打开 `ms-settings:defaultapps`。

**Architecture:** 复用已有 `SettingsLauncher.OpenDefaultApps()`；在 `App.ShowTrayContextMenu` 中插入菜单项；可选经 `MainViewModel` 暴露命令以便状态提示与测试。不引入管理员提权或 UCPD 绕过。

**Tech Stack:** .NET 8 WPF、Hardcodet.NotifyIcon.Wpf、xUnit

**Design:** `docs/plans/2026-08-03-tray-default-apps-shortcut-design.md`

---

### Task 1: ViewModel 命令打开默认应用页

**Files:**
- Modify: `src/BrowserSwitcher/ViewModels/MainViewModel.cs`
- Modify: `tests/BrowserSwitcher.Tests/`（新建或扩展测试文件）
- Test: `tests/BrowserSwitcher.Tests/SettingsLauncherTests.cs` 或新建 `MainViewModelTrayShortcutTests.cs`

**Step 1: 确认 SettingsLauncher 已有 OpenDefaultApps**

已有：`src/BrowserSwitcher/Services/SettingsLauncher.cs` 的 `OpenDefaultApps()`。

**Step 2: 在 MainViewModel 增加命令**

- 属性：`ICommand OpenDefaultAppsSettingsCommand`
- 构造中：`OpenDefaultAppsSettingsCommand = new RelayCommand(_ => OpenDefaultAppsSettings());`
- 方法：

```csharp
/// <summary>
/// 打开系统默认应用设置页（托盘快捷跳转）。
/// </summary>
public void OpenDefaultAppsSettings()
{
    try
    {
        _settingsLauncher.OpenDefaultApps();
        StatusMessage = "已打开系统默认应用设置，请选择目标浏览器。";
    }
    catch (Exception ex)
    {
        StatusMessage = $"无法打开系统设置：{ex.Message}";
        MessageBox.Show(
            $"无法打开系统默认应用设置页。\n\n{ex.Message}\n\n"
            + "请手动打开：设置 → 应用 → 默认应用。",
            "浏览器切换器",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}
```

**Step 3: 补测（注入假 SettingsLauncher 若当前构造不易测可跳过并在 Task 3 手工验）**

若 `MainViewModel` 测试成本高：至少保证 `SettingsLauncherTests` 仍覆盖 `OpenDefaultApps` 构建 ProcessStartInfo；本功能以托盘接线为主。

可选：用可注入的 `Action`/`SettingsLauncher` 子类记录是否调用。

**Step 4: Commit**

```powershell
git add src/BrowserSwitcher/ViewModels/MainViewModel.cs tests/
git commit -m "feat: add OpenDefaultAppsSettings command for tray shortcut"
```

---

### Task 2: 托盘菜单插入快捷项

**Files:**
- Modify: `src/BrowserSwitcher/App.xaml.cs`（`ShowTrayContextMenu`）

**Step 1: 在浏览器列表之后、打开主窗口之前插入**

参考现有结构（约 147–185 行），在浏览器 `foreach` 结束后：

```csharp
menu.Items.Add(new Separator());

var settingsItem = new MenuItem { Header = "打开默认应用设置…" };
ApplyTrayMenuItemTheme(settingsItem);
settingsItem.Click += (_, _) => _viewModel?.OpenDefaultAppsSettings();
menu.Items.Add(settingsItem);

var openItem = new MenuItem { Header = "打开主窗口" };
// ... 现有逻辑不变
```

确保对新项调用 `ApplyTrayMenuItemTheme`，与现有菜单对比度一致。

**Step 2: 手工运行验证菜单顺序与点击**

```powershell
dotnet run --project src/BrowserSwitcher
```

预期：托盘右键见「打开默认应用设置…」，点击打开系统设置，无 UAC。

**Step 3: Commit**

```powershell
git add src/BrowserSwitcher/App.xaml.cs
git commit -m "feat: add tray menu shortcut to default apps settings"
```

---

### Task 3: 文档与版本说明

**Files:**
- Modify: `README.md`（托盘小节补一句）
- Modify: `src/BrowserSwitcher/Resources/ReleaseNotes.md`
- Create: `docs/releases/1.3.2.md`（或按当前版本递增修订号）
- Modify: `src/BrowserSwitcher/BrowserSwitcher.csproj` Version

**Step 1: 按 VERSIONING 将版本改为下一修订号（当前若为 1.3.1 → 1.3.2）**

**Step 2: 用户可见说明**

- README 托盘：右键含「打开默认应用设置…」
- ReleaseNotes：新增托盘快捷打开系统默认应用页

**Step 3: 全量测试**

```powershell
dotnet test BrowserSwitcher.sln -c Release --verbosity minimal
```

预期：全部通过。

**Step 4: Commit**

```powershell
git add README.md docs/releases/ src/BrowserSwitcher/
git commit -m "docs: note tray default-apps shortcut in 1.3.2"
```

---

### Task 4: （可选，用户要求时）打包装

```powershell
$ver = "1.3.2"
$out = "dist\BrowserSwitcher-$ver-win-x64"
dotnet publish src/BrowserSwitcher/BrowserSwitcher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $out
Compress-Archive -Path "$out\BrowserSwitcher.exe" -DestinationPath "dist\BrowserSwitcher-$ver-win-x64.zip" -Force
```
