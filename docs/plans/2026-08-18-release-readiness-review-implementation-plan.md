# Release Readiness Review Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Install a universal `release-readiness-review` skill that produces evidence-backed, adaptive final release reviews and commit-ready audit records.

**Architecture:** The skill is a concise Markdown workflow plus a reusable audit-record template. It discovers evidence from the target repository, selects a lightweight or full profile from actual risk signals, records every one of five review dimensions, self-checks whether the recommendation is supported, and never turns a recommendation into a mechanical deployment block.

**Tech Stack:** Markdown, YAML, PowerShell, Python skill-creator utilities, generic Git and repository-native commands.

---

### Task 1: Capture RED-phase baseline evidence

**Files:**
- Create: `docs/plans/2026-08-18-release-readiness-review-baseline.md`

**Step 1: Record the three no-skill scenarios and outputs**

Document the task wording and the following observed behavior:

- A README typo review treated a permanent record as optional and assumed unverified repository conditions.
- An emergency authentication-bypass review recommended a hard release block, despite the required advisory-only model, and filled gaps with hypothetical controls.
- An API/deployment review assumed that an ADR remained authoritative instead of recording the source conflict under the agreed evidence hierarchy.

**Step 2: Define the failing acceptance criteria**

State that a passing skill must require a record for every run, report missing evidence without assumptions, use `recommend stopping release` rather than a technical block for confirmed severe risk, and record source conflicts without inventing an authority decision.

**Step 3: Verify the RED artifact**

Run: `Get-Content -Raw docs/plans/2026-08-18-release-readiness-review-baseline.md`

Expected: The artifact links each observed failure to a specific skill rule that the GREEN phase must add.

### Task 2: Initialize the personal skill directory

**Files:**
- Create: `C:\Users\admin\.agents\skills\release-readiness-review\SKILL.md`
- Create: `C:\Users\admin\.agents\skills\release-readiness-review\agents\openai.yaml`
- Create: `C:\Users\admin\.agents\skills\release-readiness-review\assets\`

**Step 1: Generate the scaffold**

Run:

```powershell
python C:\Users\admin\.codex\skills\.system\skill-creator\scripts\init_skill.py release-readiness-review --path C:\Users\admin\.agents\skills --resources assets --interface 'display_name=Release Readiness Review' --interface 'short_description=Evidence-backed final release review' --interface 'default_prompt=Use $release-readiness-review to prepare an evidence-backed final release review.'
```

**Step 2: Verify the scaffold**

Run:

```powershell
Get-ChildItem C:\Users\admin\.agents\skills\release-readiness-review -Recurse -File
```

Expected: `SKILL.md`, `agents/openai.yaml`, and the `assets` directory exist. The target is a personal skill directory, not a Git repository, so it is deliberately not committed from this project.

### Task 3: Add the commit-ready audit record template

**Files:**
- Create: `C:\Users\admin\.agents\skills\release-readiness-review\assets\release-review-record-template.md`

**Step 1: Write the template**

Include these exact sections: review metadata; change scope and profile rationale; evidence inventory; five-dimension matrix; executed commands with exit codes and material output; evidence gaps; risk register; advisory release recommendation; history and feedback follow-up; and human-approval-only skill improvement suggestions.

Use these five matrix rows: Specification, Facts, Quality, Regression, and User feedback. Give each row fields for status, evidence, findings, and follow-up. Limit status values to `verified`, `not applicable`, `evidence missing`, and `risk found`.

**Step 2: Verify the template shape**

Run:

```powershell
$headings = @('Review metadata', 'Profile rationale', 'Specification', 'Facts', 'Quality', 'Regression', 'User feedback', 'Release recommendation', 'requires human approval'); $template = Get-Content -Raw C:\Users\admin\.agents\skills\release-readiness-review\assets\release-review-record-template.md; $headings | Where-Object { $template -notmatch [regex]::Escape($_) }
```

Expected: No output. Any missing heading is a failing check and must be added before continuing.

### Task 4: Write the minimal `SKILL.md` GREEN implementation

**Files:**
- Modify: `C:\Users\admin\.agents\skills\release-readiness-review\SKILL.md`

**Step 1: Define discovery metadata**

Use only `name` and `description` in the YAML frontmatter:

```yaml
---
name: release-readiness-review
description: Use when preparing a software release or final change approval and needing an evidence-backed review of scope, quality, regression risk, requirements, or user feedback.
---
```

**Step 2: Add the evidence-first rules**

Require the reviewer to discover the change baseline and use this evidence precedence: supplied task/Issue/PR, approved specification or ADR/repository documentation, then contracts/code/tests/history. Require conflict recording rather than invented resolution. Require only repository-discoverable commands and factual reporting of missing inputs or failed commands.

**Step 3: Add the first self-check and two profiles**

Define `lightweight` only for independently verifiable, local, low-risk changes. Define `full` for public contracts, data/schema/migrations, authentication/security/privacy, dependency changes, concurrency/reliability/performance, delivery configuration, or feedback-linked fixes. Require `full` when evidence is insufficient to classify reliably.

**Step 4: Add the five-dimension review workflow**

Require a status and supporting evidence for Specification, Facts, Quality, Regression, and User feedback on every run. Permit `not applicable` only with an evidence-backed rationale. Require user feedback to be searched only in accessible issues, review comments, defect/support/community/product feedback, and require `evidence missing` when none is accessible.

**Step 5: Add the second self-check and recommendations**

Before reporting, require each conclusion, severity, and recommendation to trace to recorded evidence; remove unsupported claims. Use only these non-binding recommendations: `ready to release`, `ready to release with listed risks`, `evidence insufficient; supplement before deciding`, and `recommend stopping release`. Clarify that even the final recommendation leaves the release decision to a human.

**Step 6: Require the history loop and record output**

Direct the reviewer to use existing repository conventions or copy the asset template to `docs/release-reviews/YYYY-MM-DD-<change-id>-<summary>.md`. Require prior related records to be considered, `requires human approval` for every skill-improvement idea, and no automatic staging, committing, or self-modification.

**Step 7: Add operational guardrails**

Explicitly forbid treating a small diff as grounds to skip a record, treating urgency as evidence, calling a missing test successful, assuming source authority during a conflict, and mechanically blocking a release. Include a short quick-reference table of profile/risk/action and a concise common-mistakes section.

### Task 5: Generate and validate user-facing metadata

**Files:**
- Modify: `C:\Users\admin\.agents\skills\release-readiness-review\agents\openai.yaml`

**Step 1: Regenerate metadata from the completed skill**

Run:

```powershell
python C:\Users\admin\.codex\skills\.system\skill-creator\scripts\generate_openai_yaml.py C:\Users\admin\.agents\skills\release-readiness-review --interface 'display_name=Release Readiness Review' --interface 'short_description=Evidence-backed final release review' --interface 'default_prompt=Use $release-readiness-review to prepare an evidence-backed final release review.'
```

**Step 2: Run structural validation**

Run:

```powershell
python C:\Users\admin\.codex\skills\.system\skill-creator\scripts\quick_validate.py C:\Users\admin\.agents\skills\release-readiness-review
```

Expected: The validator reports success with no frontmatter or naming errors.

### Task 6: Run GREEN forward tests against the same scenarios

**Files:**
- Modify: `docs/plans/2026-08-18-release-readiness-review-baseline.md`

**Step 1: Re-run the README typo scenario using the installed skill**

Expected: It selects a lightweight review, still requires a commit-ready record, marks unavailable tests/feedback as not applicable or evidence missing with rationale, and makes no repository-policy assumptions.

**Step 2: Re-run the emergency authentication-bypass scenario using the installed skill**

Expected: It selects a full review, records the absent specification and ticket as evidence gaps, and can emit `recommend stopping release` only as a human-decision advisory.

**Step 3: Re-run the conflicting API/deployment scenario using the installed skill**

Expected: It selects a full review, records the ADR/PR conflict, makes no unverified authority claim, records CI and feedback evidence gaps, and ties every recommendation to evidence.

**Step 4: Record the observed GREEN behavior**

Append a pass/fail result for each criterion to the baseline artifact. If any scenario invents a fact, skips a record, mechanically blocks release, or omits a required dimension, return to Task 4 and add the smallest rule that corrects it.

### Task 7: Verify deployment and preserve the project checkpoint

**Files:**
- Modify: `docs/plans/2026-08-18-release-readiness-review-baseline.md`
- Create: `docs/plans/2026-08-18-release-readiness-review-implementation-plan.md`

**Step 1: Perform final checks**

Run:

```powershell
python C:\Users\admin\.codex\skills\.system\skill-creator\scripts\quick_validate.py C:\Users\admin\.agents\skills\release-readiness-review
git diff --check
git status --short
```

Expected: Skill validation succeeds; the worktree diff has no whitespace errors; only the two planning artifacts are staged for the project commit.

**Step 2: Commit the project-side evidence**

Run:

```powershell
git add docs/plans/2026-08-18-release-readiness-review-baseline.md docs/plans/2026-08-18-release-readiness-review-implementation-plan.md
git commit -m "docs(skills): 记录发布审核技能验证"
```

Expected: The plan and RED/GREEN test evidence have a dedicated project commit. The installed personal skill remains available at `C:\Users\admin\.agents\skills\release-readiness-review`.
