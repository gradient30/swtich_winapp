# Release Readiness Review Chinese Output Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make release-review records Chinese by default while preserving raw technical evidence and untranslatable terms.

**Architecture:** Update the reusable Markdown template so all human-facing narrative and status fields are Chinese. Update the skill workflow to mandate that policy while keeping identifiers, commands, paths, error codes, and raw command output verbatim; a separate Chinese interpretation explains raw output without replacing it.

**Tech Stack:** Markdown, YAML, PowerShell, Python skill-creator validator.

---

### Task 1: Establish the failing Chinese-output contract

**Files:**
- Test: `C:\Users\admin\.agents\skills\release-readiness-review\assets\release-review-record-template.md`

**Step 1: Check the current template for required Chinese fields**

Run:

```powershell
$template = Get-Content -Raw 'C:\Users\admin\.agents\skills\release-readiness-review\assets\release-review-record-template.md'; $required = @('审核元数据', '审查档位判定依据', '五维审核矩阵', '原始命令', '原始输出', '中文解读', '发布建议', '需人工确认'); $missing = $required | Where-Object { $template -notmatch [regex]::Escape($_) }; if (-not $missing) { throw 'Expected Chinese-output contract to fail before implementation.' }; $missing
```

Expected: FAIL or list missing Chinese fields because the existing template is English.

### Task 2: Localize the audit-record template without changing evidence

**Files:**
- Modify: `C:\Users\admin\.agents\skills\release-readiness-review\assets\release-review-record-template.md`

**Step 1: Translate human-facing structure**

Translate the title, headings, table labels, review statuses, recommendations, and narrative placeholders into Chinese. Use Chinese status values: `已验证`, `不适用`, `证据缺失`, and `发现风险`.

**Step 2: Preserve technical evidence**

Use an executed-command table with the exact columns `原始命令`, `声明来源`, `退出码`, `原始输出`, and `中文解读`. Require raw output to remain verbatim and place the Chinese explanation in its own column.

**Step 3: Verify the contract passes**

Run the Task 1 command again.

Expected: No missing fields and no error.

### Task 3: Add Chinese-output rules to the skill workflow

**Files:**
- Modify: `C:\Users\admin\.agents\skills\release-readiness-review\SKILL.md`

**Step 1: Require Chinese as the canonical output language**

Add a concise `输出语言` section that requires all non-code narrative, statuses, conclusions, risk descriptions, recommendations, and log interpretations to be Chinese by default.

**Step 2: Define evidence-preservation boundaries**

Require code, identifiers, file paths, commands, exit codes, API names, error codes, and untranslatable terms to remain unchanged. Require raw command output to remain verbatim, with a Chinese explanation beside it; prohibit replacing factual output with a translation.

**Step 3: Eliminate duplicate-document ambiguity**

State that the Chinese Markdown record is the only canonical audit record. Do not produce a parallel English record merely because raw evidence is English.

### Task 4: Regenerate metadata and validate structure

**Files:**
- Modify: `C:\Users\admin\.agents\skills\release-readiness-review\agents\openai.yaml`

**Step 1: Use a Chinese default prompt**

Run:

```powershell
python C:\Users\admin\.codex\skills\.system\skill-creator\scripts\generate_openai_yaml.py C:\Users\admin\.agents\skills\release-readiness-review --interface 'display_name=发布就绪审核' --interface 'short_description=基于证据的最终发布审核' --interface 'default_prompt=使用 $release-readiness-review 对当前变更执行中文发布审核并生成记录。'
```

**Step 2: Run structural validation**

Run:

```powershell
python C:\Users\admin\.codex\skills\.system\skill-creator\scripts\quick_validate.py C:\Users\admin\.agents\skills\release-readiness-review
```

Expected: Validator reports success.

### Task 5: Forward-test Chinese narration and raw-evidence fidelity

**Files:**
- Test: `C:\Users\admin\.agents\skills\release-readiness-review\SKILL.md`
- Test: `C:\Users\admin\.agents\skills\release-readiness-review\assets\release-review-record-template.md`

**Step 1: Use the skill on an English-output scenario**

Prompt: `Use $release-readiness-review to audit a local README fix. The discovered command is dotnet test and its output is "Build succeeded. Tests passed: 36". Do not create files; show the required record fields.`

**Step 2: Verify behavior**

Expected: The review narrative, statuses, recommendation, and log interpretation are Chinese; `dotnet test` and `Build succeeded. Tests passed: 36` remain verbatim; no separate English record is proposed.

### Task 6: Preserve project-side evidence

**Files:**
- Create: `docs/plans/2026-08-18-release-readiness-review-chinese-output-implementation-plan.md`

**Step 1: Check project changes**

Run:

```powershell
git diff --check
git status --short
```

Expected: No whitespace errors; only the implementation-plan document is a project-side change.

**Step 2: Commit the plan evidence**

Run:

```powershell
git add docs/plans/2026-08-18-release-readiness-review-chinese-output-implementation-plan.md
git commit -m "docs(skills): 记录中文输出实施计划"
```

Expected: A dedicated commit records the update plan; the installed personal skill remains outside this repository.
