# 版本发布约定

## 版本号

采用语义化版本 `主.次.修订`（与 `BrowserSwitcher.csproj` 中 `<Version>` 一致）：

| 变更类型 | 递增 | 示例 |
| --- | --- | --- |
| 不兼容的重大调整 | 主版本 | 1.x → 2.0.0 |
| 新功能、明显体验升级 | 次版本 | 1.0.0 → 1.1.0 |
| 缺陷修复、文案/小优化 | 修订号 | 1.1.0 → 1.1.1 |

同步修改：

1. `src/BrowserSwitcher/BrowserSwitcher.csproj` 的 `Version` / `AssemblyVersion` / `FileVersion`
2. 新增 `docs/releases/x.y.z.md`（面向用户的更新说明）
3. 将同内容同步到 `src/BrowserSwitcher/Resources/ReleaseNotes.md`（供程序内「?」帮助读取）
4. 发布包目录/压缩包名带上版本号，例如 `BrowserSwitcher-1.1.0-win-x64.zip`

## 更新说明写法（给用户看）

- 只写用户能感知的变化
- 功能、缺陷修复可分条列出
- 纯性能/内部重构用一句话带过，勿展开技术细节
- 控制在数条以内，避免发版说明说明书化

## 文件模板

见同目录下任一 `x.y.z.md`。
