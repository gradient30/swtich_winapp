# 浏览器切换器（Browser Switcher）

Windows 托盘小工具：检测 Brave / Chrome / IE / Edge，从托盘或主窗口快速切换默认浏览器。优先走系统允许的入口；本机若放有社区工具则可尝试直切。**不会**以禁用 UCPD 或改写 UserChoice 哈希作为产品方案。

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
# 将版本号换成当前 Version
$ver = "1.3.0"
$out = "dist\BrowserSwitcher-$ver-win-x64"
dotnet publish src/BrowserSwitcher/BrowserSwitcher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $out
Compress-Archive -Path "$out\BrowserSwitcher.exe" -DestinationPath "dist\BrowserSwitcher-$ver-win-x64.zip" -Force
```

发布产物示例：

`dist/BrowserSwitcher-1.3.0-win-x64.zip`

## 使用说明

### 托盘

- 启动后默认仅显示托盘图标（提示含当前默认浏览器）
- **左键**：显示 / 隐藏主窗口
- **右键菜单**：快捷切换浏览器、「打开默认应用设置…」、「打开主窗口」、「退出」
- 关闭主窗口会隐藏到托盘；只有托盘菜单中的「退出」才会结束进程

### 主窗口

- 查看各浏览器状态（已安装 / 手动指定 / 未找到）
- **设为默认**：按下方策略尝试切换，失败则打开对应系统设置页并提示
- **指定路径**：为未找到或需覆盖的浏览器选择 `.exe`，写入 `%AppData%\BrowserSwitcher\config.json`

### 设置页

- 居中紧凑面板：查看与编辑已保存的手动路径
- **开机启动**：保存时写入当前用户 `Run` 注册表项；取消不生效
- 显示程序集版本号

### 默认浏览器切换策略

微软要求经系统 UI 确认默认应用（Windows 8+ 起写入 API 基本不可用；2024 起 UCPD 还会拦截对 UserChoice 的第三方直接改写）。本工具策略：

1. 若程序目录或 `Tools\` 下存在社区工具 [`SetDefaultBrowser`](https://kolbi.cz/blog/2017/11/10/setdefaultbrowser-set-the-default-browser-per-user-on-windows-10-and-server-2016-build-1607/)（**不随本应用分发**），则优先调用尝试直切。该工具在较新 Windows / UCPD 环境下可能失败，失败会自动回退。
2. 兼容路径：尝试旧版 COM `SetAppAsDefault`（多数新系统会失败）。
3. 官方路径：打开该应用的关联设置 UI，或 Win11 深链 `ms-settings:defaultapps?registeredAppMachine|User=...`，由你点一次「设为默认」。
4. 仍失败则打开通用「默认应用」页。

本应用自身不编写受保护的 `UserChoice` 注册表；若调用了本机已有的 SetDefaultBrowser，由其自行处理（成功率视系统而定）。

## 测试

```powershell
dotnet test BrowserSwitcher.sln
```
