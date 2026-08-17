# 浏览器切换器（Browser Switcher）

Windows 托盘小工具：检测 Brave / Chrome / IE / Edge，从托盘或主窗口切换默认浏览器。

**默认模式（1.4.0+）**：开启「托盘一键直切」后，优先通过内置 UserChoice 直写实现静默切换；失败时给出 UCPD / 外部工具提示，不自动打开设置页。关闭该开关则回退为「社区工具 → COM → 系统设置 UI」的合规路径。

## 要求

- Windows 10 / 11（x64）
- 开发构建需 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## 快速开始

```powershell
dotnet run --project src/BrowserSwitcher
```

## 发布

当前版本见 `src/BrowserSwitcher/BrowserSwitcher.csproj` 中的 `<Version>`。

```powershell
$ver = "1.4.0"
$out = "dist\BrowserSwitcher-$ver-win-x64"
dotnet publish src/BrowserSwitcher/BrowserSwitcher.csproj -c Release -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o $out
Compress-Archive -Path "$out\BrowserSwitcher.exe" -DestinationPath "dist\BrowserSwitcher-$ver-win-x64.zip" -Force
```

版本约定与更新日志：[docs/releases/VERSIONING.md](docs/releases/VERSIONING.md)、[docs/releases/CHANGELOG.md](docs/releases/CHANGELOG.md)。

## 使用

| 入口 | 行为 |
| --- | --- |
| 托盘左键 | 显示 / 隐藏主窗口 |
| 托盘右键 → 浏览器项 | 切换默认浏览器（见下方策略） |
| 托盘右键 → 打开默认应用设置… | `ms-settings:defaultapps` |
| 主窗口「设为默认」 | 同托盘切换逻辑 |
| 设置 → 托盘一键直切 | 开关 `ForceDirectSwitch`（默认开） |
| 设置 → UCPD | 检测 / 提权禁用 UCPD（需重启生效） |

配置持久化：`%AppData%\BrowserSwitcher\config.json`。

## 切换策略（概要）

详细原理见 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)。

**一键直切开启时**（`ForceDirectSwitch = true`，默认）：

1. 内置 `UserChoiceAssociationWriter`：写入 `http` / `https` 的 ProgId + Hash
2. 提权调用 `Tools\SetUserFTA.exe`（若存在）
3. 提权调用 `Tools\SetDefaultBrowser.exe`（若存在）
4. COM `SetAppAsDefault`（多数新系统无效）
5. 全部失败 → 弹窗提示（含 UCPD / 工具放置说明），**不**打开设置页

**一键直切关闭时**（合规回退）：

1. 非提权调用本机 `SetDefaultBrowser.exe`（若存在）
2. COM → 关联 UI → `ms-settings` 深链 → 默认应用总页

可选工具**不随应用分发**，需自行放入程序目录或 `Tools\` 子目录。

## 测试

```powershell
dotnet test BrowserSwitcher.sln
```

实机切换集成测试（会改写本机 UserChoice）：

```powershell
$env:RUN_LIVE_SWITCH_TESTS = "1"
dotnet test --filter "FullyQualifiedName~LiveSwitch"
```

## 文档

| 文件 | 内容 |
| --- | --- |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | 架构、模块、切换核心原理（UserChoice / UCPD / Hash） |
| [docs/releases/CHANGELOG.md](docs/releases/CHANGELOG.md) | 版本更新历史 |
| [docs/releases/VERSIONING.md](docs/releases/VERSIONING.md) | 发版流程 |
