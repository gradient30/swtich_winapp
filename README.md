# 浏览器切换器（Browser Switcher）

Windows 托盘小工具：检测 Brave / Chrome / IE / Edge，一键打开系统「默认应用」设置页并提示选择目标浏览器。**不会**静默修改默认浏览器关联。

## 要求

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## 运行

在仓库根目录执行：

```powershell
dotnet run --project src/BrowserSwitcher
```

## 发布

版本约定见 [`docs/releases/VERSIONING.md`](docs/releases/VERSIONING.md)。面向用户的更新说明在 `docs/releases/x.y.z.md`，发版时同步到 `src/BrowserSwitcher/Resources/ReleaseNotes.md`。

```powershell
# 将 1.1.0 换成当前 Version
$ver = "1.2.0"
$out = "dist\BrowserSwitcher-$ver-win-x64"
dotnet publish src/BrowserSwitcher/BrowserSwitcher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $out
Compress-Archive -Path "$out\BrowserSwitcher.exe" -DestinationPath "dist\BrowserSwitcher-$ver-win-x64.zip" -Force
```

发布产物示例：

`dist/BrowserSwitcher-1.1.0-win-x64.zip`

## 使用说明

### 托盘

- 启动后默认仅显示托盘图标（提示文字：`浏览器切换器`）
- **左键**：显示 / 隐藏主窗口
- **右键菜单**：快捷选择浏览器、「打开主窗口」、「退出」
- 关闭主窗口会隐藏到托盘；只有托盘菜单中的「退出」才会结束进程

### 主窗口

- 查看各浏览器状态（已安装 / 手动指定 / 未找到）
- **设为默认**：打开 `ms-settings:defaultapps`，并在底部提示选择对应浏览器
- **指定路径**：为未找到或需覆盖的浏览器选择 `.exe`，写入 `%AppData%\BrowserSwitcher\config.json`

### 设置页

- 查看与编辑已保存的手动路径
- **开机启动**：写入当前用户 `Run` 注册表项
- 显示程序集版本号

### 系统设置确认

本工具只负责打开系统默认应用页。请在系统设置中手动确认目标浏览器为默认 Web 浏览器；应用不会改写 `UserChoice` 等关联注册表。

## 测试

```powershell
dotnet test BrowserSwitcher.sln
```
