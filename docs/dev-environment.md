# 开发环境注意事项（本机排障记录）

> 适用：本仓库的主要开发机（Win10 19045）。供换机 / 重装 / 排障时参考。
> 相关记录：[1.4.1 部署诊断](release-reviews/2026-10-27-1.4.1-deployment-diagnosis.md)（UCPD / UserChoice ACL 排查）。

## git push 报 `not a git repository` 的坑

### 症状

任何仓库执行 `git push`（无论参数形式）一律报：

```text
fatal: not a git repository (or any of the parent directories): .git
```

而 `git log` / `status` / `fetch` / `ls-remote` / `commit` 全部正常。兄弟仓库同样复现，与具体仓库无关。

### 根因

`git push` 特有的「从当前目录向上发现 `.git`」链路在本机失效（`GIT_TRACE2` 显示 git 进程在 `cmd_name` 事件之前即 die）。Program Files Git 2.55.0.windows.3 与 scoop git 2.55.0 **均复现**，换发行版无效；显式 `-C <绝对路径>` 完全绕开 CWD 依赖后两个发行版均正常。

### 解法（二选一）

**方式一：显式 `-C`（任何 shell 通用）**

```bash
git -C "D:/workspace_test/github_repo/swtich_winapp" push origin main
```

**方式二：PowerShell 里定义 `gpush`（本机 profile 已内置）**

```powershell
# 定义于 $PROFILE（Documents\WindowsPowerShell\Microsoft.PowerShell_profile.ps1）
function gpush {
    $env:GIT_INSTALL_ROOT = 'C:\Users\admin\scoop\apps\git\2.55.0'
    & (Join-Path $env:GIT_INSTALL_ROOT 'mingw64\bin\git.exe') -C (Get-Location).Path push @args
}
```

日常在仓库目录里直接 `gpush` 即可。

### 本机 git 安装情况

- Program Files Git 2.55.0.windows.3（PATH 默认命中）
- scoop git：`C:\Users\admin\scoop\apps\git\`（2.53.0 / 2.55.0 并存，`current` 指向其一）

两套混装本身不是本坑的根因（见上），但升级时注意 `current` 链接指向。

## 其他

- 默认浏览器直写的环境前提与排查步骤见 [1.4.1 部署诊断](release-reviews/2026-10-27-1.4.1-deployment-diagnosis.md)。
- 实机切换测试会真实改写本机默认浏览器关联：`RUN_LIVE_SWITCH_TESTS=1 dotnet test --filter "FullyQualifiedName~LiveSwitch"`（结束后恢复 Brave，测试有断言）。
