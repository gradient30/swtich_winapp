# 版本发布约定

## 版本号

语义化版本 `主.次.修订`，与 `BrowserSwitcher.csproj` 中 `<Version>` 一致：

| 变更类型 | 递增 | 示例 |
| --- | --- | --- |
| 不兼容的重大调整 | 主版本 | 1.x → 2.0.0 |
| 新功能、明显体验升级 | 次版本 | 1.0.0 → 1.1.0 |
| 缺陷修复、文案/小优化 | 修订号 | 1.1.0 → 1.1.1 |

## 发版检查清单

1. 更新 `src/BrowserSwitcher/BrowserSwitcher.csproj` 的 `Version` / `AssemblyVersion` / `FileVersion`
2. 在 [CHANGELOG.md](CHANGELOG.md) 顶部追加新版本条目（用户可感知的变化）
3. 将**当前版本**说明同步到 `src/BrowserSwitcher/Resources/ReleaseNotes.md`（程序内「?」帮助）
4. 发布包命名：`dist/BrowserSwitcher-{版本}-win-x64.zip`

## 更新说明写法

- 只写用户能感知的变化
- 控制在数条以内
- 架构与原理变更写入 [ARCHITECTURE.md](../ARCHITECTURE.md)，勿在 CHANGELOG 展开技术细节
