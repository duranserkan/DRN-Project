---
description: Verification phase of /update — validate skill-body content against actual source code, staged per project family
---

> **Sub-workflow of `/update`** — invoked after execution and review, never directly.
> Reads `Scope` from plan header. Generates stages only for executed stages; skipped stages carry over as `skipped`.
>
> **Estimated context: ~2.5K tokens** (this workflow)

---

## 0. Initialize Progress File

// turbo
```
view_file .agent/update-verify-progress.md
# Not found → create from template (§0.1)
# Found → resume from first non-done stage (skip pass, fail, skipped, blocked)
```

### 0.1 Progress File Template

**Location**: `.agent/update-verify-progress.md`

````markdown
# Update Verification Progress

> Generated: <ISO-8601 timestamp>
> Status: verifying | verified | failed
> Plan: .agent/update-plan.md

---

## Stage 0: Structural Integrity
> Status: pending | skipped | executing | pass | fail | blocked

### Checks
_(per §1)_

### Findings

| Check | Status | Correction |
|-------|--------|------------|

---

## Stage 1: Non-Project Asset Verification
> Status: pending | skipped | executing | pass | fail | blocked

### Checks
_(per §2)_

### Findings

| Check | Status | Correction |
|-------|--------|------------|

---

## Stages 2–N: Per-Project-Family Verification
> **Dynamic** — one stage per project-family prefix from Projects Manifest.
> Generate only for affected families; unaffected → `skipped`.

#### Stage Generation Rule

For each distinct family prefix in Discovery Summary → Projects Manifest:
1. Title: `Stage <N>: <FamilyPrefix>.* Verification`
2. List projects under `> Projects:`
3. Apply per-family checks (§3)

> [!IMPORTANT]
> Never hardcode families. Generate only what the manifest reports.

#### Per-Family Stage Template

```markdown
## Stage N: <FamilyPrefix>.* Verification
> Status: pending | skipped | executing | pass | fail | blocked
> Projects: <list from manifest>
> Last verified skill: *(updated after each skill — enables mid-stage resumption)*

### Checks
_(per §3.2)_

### Skills Checked
_(list each skill and result)_

### Findings

| Skill | Reference | Type | Status | Correction |
|-------|-----------|------|--------|------------|
```

> **Example**: DRN-Project → 4 families = Stages 2–5; single-family repo → 1 stage.

---

## Stage Final: Verdict
> Status: pending | executing | pass | fail
> Stage number: last per-family stage + 1 (e.g., if families end at Stage 5, this is Stage 6)

### Summary
| Stage | Status | Findings |
|-------|--------|----------|
| 0 — Structural | ⏳ | — |
| 1 — Non-Project Assets | ⏳ | — |
| _N — <Family>_ | ⏳ | — |
| **Final — Verdict** | ⏳ | — |

### Verdict
- **Overall**: ⏳ pending
- **Action required**: _(populated during execution)_

### Corrections Required _(if verdict is ❌ or ⚠️)_

| # | Stage | Skill | Reference | Type | Correction |
|---|-------|-------|-----------|------|------------|
| _(consolidated from per-stage findings during verdict)_ | | | | | |
````

---

## 1. Stage 0 — Structural Integrity

Gate check — catches regressions introduced during review→commit.

### 1.1 Workflow ↔ Skill Directory Consistency

```
list_dir .agent/skills
# Each directory → verify presence in primary group workflow (bidirectional)
```

### 1.2 `all.md` Union Validation

```
# Collect skills from group workflows per Standard Load Order (update.md §Standard Load Order)
# + custom groups from plan header — never hardcode group names
# Exclude task-workflow skill sections (review.md §2, update.md)
# Compare against all.md entries; verify section order matches load order
```

### 1.3 Token Estimate Verification

For each group workflow:
1. Sum file sizes of referenced `SKILL.md` files ÷ 4
2. Compare with `Estimated context:` line — **flag** if delta > 10%
3. Verify `all.md` total ≈ sum of group estimates

### 1.4 Cross-Reference Integrity

Detect cross-references (skills whose directory prefix ≠ workflow group):
- Verify against plan's drift report
- Flag undocumented additions/removals

### 1.5 AGENTS.md & Skill Index

- `AGENTS.md` project references → resolve to actual `*.csproj`
- `overview-skill-index/SKILL.md` → references only existing skill directories

### Completion

Set Stage 0 to `pass` or `fail`.

> Stage 0 **failure blocks all subsequent stages**. Mark remaining stages `blocked` (terminal — resume logic skips them). After fixes, re-run Stage 0; on pass, reset blocked → `pending`.

---

## 2. Stage 1 — Non-Project Asset Verification

### 2.1 Identify Relevant Skills

Baseline: plan's non-project assets manifest. Scan `.agent/skills/*/SKILL.md` for references to:

- `Directory.Build` / `Directory.Packages` / `.github/workflows`
- `Dockerfile` / `docker-compose` / `global.json` / `nuget.config` / `.editorconfig`

### 2.2 Verify References

For each reference:
1. Verify file exists
2. Spot-check documented specifics (framework version, CI step name) against actual
3. Flag stale references

Set Stage 1 to `pass` or `fail`.

---

## 3. Stages 2–N — Per-Project Content Verification

Verify skills accurately describe current source for each project-family stage.

### 3.1 Identify Relevant Skills

```
grep_search "<FamilyPrefix>" .agent/skills/*/SKILL.md
```

### 3.2 Verify Code Identifiers

For each skill, extract and verify:

| Reference Type | Extraction | Verification |
|---------------|-----------|--------------|
| Class/interface names | Backtick-quoted, code blocks | `grep_search` in source |
| File paths | Inline/code block paths | `find_by_name` or `view_file` |
| Namespace references | `namespace`/`using` in code | `grep_search` in source |
| Method/property names | Backtick-quoted, examples | `grep_search` in source |
| Configuration keys | Settings, `IAppSettings` keys | `grep_search` in `appsettings*.json` + source |
| Attribute names | `[Scoped]`, `[Config]`, custom | `grep_search` in source |

### 3.3 Verify Pattern Accuracy

For skills documenting architectural patterns:
1. Compare pattern description against representative source implementation
2. Flag divergence

### 3.4 Stage Completion

Set stage status:
- `pass` — no ❌; any ⚠️ are minor (estimate delta ≤10%, cosmetic wording, non-critical path)
- `fail` — any ❌, or ⚠️ on a critical reference

> **Primary** (→ ❌): identifier consumers use to locate/use the artifact — class, interface, method, attribute, config key, canonical path. Not found in source → ❌.
>
> **Secondary** (→ ⚠️): illustrative — example path, supplementary snippet, non-misleading prose.

> [!NOTE]
> **Mid-stage checkpointing**: Record skill results as you go. Context exhaustion mid-stage → mark `executing` and stop; next `/update` resumes via §0.

---

## 4. Final Stage — Verdict

After all stages complete:

### 4.1 Aggregate & Determine Verdict

| Condition | Verdict | Action |
|-----------|---------|--------|
| All executed stages `pass`, no ⚠️ | ✅ **Verified** | Plan status → `verified` |
| All executed stages `pass`, some have ⚠️ warnings | ⚠️ **Verified with warnings** | List warnings, plan → `verified` |
| Any executed stage `fail` (contains ❌) | ❌ **Failed** | Consolidate corrections from per-stage findings into Verdict §Corrections Required; plan → `failed` |

> Skipped stages are excluded from verdict evaluation.

### 4.2 Update Plan & Report

```
# ✅ or ⚠️ → .agent/update-plan.md Status: verified
# ❌ → .agent/update-plan.md Status: failed (next /update re-enters verify)
```

```markdown
## ✅ Verification Complete

All stages passed. Skill content is aligned with source code.

**Next steps:**
1. Review `.agent/update-verify-progress.md` for details
2. Commit per `basic-git-conventions` — e.g. `git add .agent/ && git commit -m "chore(skills): sync agent configuration"`
3. Delete `.agent/update-verify-progress.md` and `.agent/update-plan.md`
```

```markdown
## ❌ Verification Failed — Corrections Required

Drift detected. The following corrections are needed before commit:

| # | Stage | Skill | Reference | Type | Correction |
|---|-------|-------|-----------|------|------------|
| 1 | _N_ | _skill-name_ | `ClassName` | Class | Renamed to `NewName` in `File.cs:L42` — update skill |

**Next steps:**
1. Apply corrections listed above (manually or via next `/update` run)
2. Re-run `/update` verify phase to confirm fixes
```

> **Stage Resumption**: Per `update.md` §Plan File Contract, using `.agent/update-verify-progress.md`. Terminal statuses: `pass`, `fail`, `skipped`, `blocked`.

---

## Design Properties

| Property | Guarantee |
|----------|-----------|
| **Non-destructive** | Read-only — generates correction recommendations but never modifies skill files or source |
| **Fail-fast** | Stage 0 failure blocks all subsequent stages |
| **Context-safe** | Each project stage fits one context window; `executing` persists for cross-window resumption |
| **Ephemeral** | `.agent/update-verify-progress.md` is temporary — delete after commit |