---
description: Verification phase of /update — validate skill-body content against actual source code, staged per project family
---

> **Sub-workflow of `/update`** — not invoked directly.
> Reads `Scope` and generates/resumes stages based on executed stages.
> **Estimated context: ~1.5K tokens**

---

## 0. Progress File Contract
**Location**: `.agent/temp/update-verify-progress.md` (ephemeral progress file).
If missing, initialize from template. If found, resume from the first actionable stage:
- `pending` / `executing`: continue normally.
- `fail`: reset to `pending` and re-run after corrections.
- `blocked`: reset to `pending` only after the blocking stage has been reset or passed.
- `pass` / `skipped`: terminal; do not re-run unless the plan scope changed.

### 0.1 Progress File Template
```markdown
# Update Verification Progress
> Generated: <timestamp> | Status: verifying | verified | failed | Plan: .agent/temp/update-plan.md

## Stage 0: Structural Integrity
> Status: pending | skipped | executing | pass | fail | blocked
### Checks & Findings

## Stage 1: Non-Project Asset Verification
> Status: pending | skipped | executing | pass | fail | blocked
### Checks & Findings

## Stages 2–N: Per-Project-Family Verification
> Status: pending | skipped | executing | pass | fail | blocked
> Projects: <list> | Last verified skill: <name>
### Checks & Findings

## Stage Final: Verdict
> Status: pending | executing | pass | fail
### Summary Table & Corrections Required Table
```

- **Dynamic Stage Generation**: Generate one verification stage per project-family prefix. Unaffected families are marked `skipped`.
- **Failed Re-Verification**: When `.agent/temp/update-plan.md` has `Status: failed`, reset failed verification stages to `pending` before resuming. If Stage 0 previously failed, reset Stage 0 first; after it passes, reset blocked later stages to `pending`.
- **Verdict Rules**:
  - `pass`: No `❌` errors; minor `⚠️` warnings allowed (estimate delta ≤ 10%, cosmetic prose).
  - `fail`: Any `❌` error or a critical `⚠️` warning.
  - *Primary (❌)*: Identifier used to locate/load code (class, interface, method, attribute, config key, path).
  - *Secondary (⚠️)*: Illustrative snippets or example paths.
- **Resumption**: Mid-stage checkpointing updates `Last verified skill` to allow context-eviction recovery.

---

## 1. Stage 0 — Structural Integrity
*Failure blocks all subsequent stages (remaining set to `blocked`)*.
- **Consistency**: Bidirectional check of `.agent/skills/` directories ↔ workflow loader listings.
- **Union Validation**: Verify `load-skills-all.md` matches loaders in Standard Load Order.
- **Token Estimate**: Check Sum of skill file sizes ÷ 4 ≈ `Estimated context:` (flag if delta > 10%).
- **Cross-References**: Verify cross-references against plan drift report.
- **References**: Resolve `AGENTS.md` project paths and `overview-skill-index` skill directories.

---

## 2. Stage 1 — Non-Project Asset Verification
Verify non-project config references (props, json, workflows, compose, configs):
1. Confirm referenced files exist in the repo.
2. Spot-check details (framework versions, step names) against actual file contents.
3. Flag stale references.

---

## 3. Stages 2–N — Per-Project Content Verification
For each project family, locate relevant skills (via `grep_search "<FamilyPrefix>"`):
1. **Verify Code Identifiers**:
   - *Classes/Interfaces*: Quoted names exist in source.
   - *File paths*: Paths exist.
   - *Namespaces*: `namespace`/`using` exist in source.
   - *Methods/Properties*: Names exist in source.
   - *Config Keys*: Key matches `appsettings*.json` or source.
   - *Attributes*: Custom/DI lifetimes exist.
2. **Verify Pattern Accuracy**: Compare architectural descriptions against representative code.

---

## 4. Final Stage — Verdict
Aggregate results across executed stages:

| Stage Statuses | Verdict | Action |
|----------------|---------|--------|
| All `pass` (no ⚠️) | ✅ **Verified** | Plan status → `verified` |
| All `pass` (some ⚠️) | ⚠️ **Verified with warnings** | List warnings, plan → `verified` |
| Any `fail` (contains ❌) | ❌ **Failed** | Consolidate corrections, plan → `failed` |

### Report Templates

#### Success (✅ or ⚠️)
```markdown
## ✅ Verification Complete
All stages passed. Skill content is aligned.
**Next steps:**
1. Review `.agent/temp/update-verify-progress.md` for warnings.
2. Delete `.agent/temp/update-plan.md` and `.agent/temp/update-verify-progress.md`.
3. Commit: `git add .agent/ && git commit -m "chore(skills): sync agent configuration"`
```

#### Failure (❌)
```markdown
## ❌ Verification Failed — Corrections Required
Drift detected. Apply corrections and re-run `/update`:
| # | Stage | Skill | Reference | Type | Correction |
|---|-------|-------|-----------|------|------------|
```

---

## 5. Design Properties
- **Non-destructive / Ephemeral**: Read-only verification; plan/progress files are removed before commit.
- **Fail-fast**: Stage 0 blocks others.
- **Context-safe**: Mid-stage checkpointing enables multi-window execution.
