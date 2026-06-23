---
description: Verification phase of /update — validate skill-body content against actual source code, staged per project family
---

> **Sub-workflow of `/update`**. Not invoked directly.
> Reads `Scope` and generates/resumes verification stages from executed stages.
> See also: [Status Lifecycle](./_shared/status-lifecycle.md) · [Operating Model](./_shared/workflow-operating-model.md)
> **Estimated context: ~1.5K tokens**

## 0. Progress File Contract

**Location**: `.agent/temp/update-verify-progress.md`.

If invoked directly, run the shared Startup Gate; otherwise inherit `/update` context.

If the file is missing, initialize it from the template. If it exists, resume from the first actionable stage:

- `pending` / `executing`: continue.
- `fail`: reset to `pending` and re-run after corrections.
- `blocked`: reset to `pending` only after the blocking stage is reset or passed.
- `pass` / `skipped`: terminal; do not re-run unless plan scope changed.

```markdown
# Update Verification Progress
> Generated: <timestamp> | Status: verifying | verified | failed | Plan: .agent/temp/update-plan.md

## Stage 0: Structural Integrity
> Status: pending | skipped | executing | pass | fail | blocked
### Checks & Findings

## Stage 1: Non-Project Asset Verification
> Status: pending | skipped | executing | pass | fail | blocked
### Checks & Findings

## Stages 2-N: Per-Project-Family Verification
> Status: pending | skipped | executing | pass | fail | blocked
> Projects: <list> | Last verified skill: <name>
### Checks & Findings

## Stage Final: Verdict
> Status: pending | executing | pass | fail | blocked
### Summary Table & Corrections Required Table
```

Rules:

- Generate one verification stage per project-family prefix; mark unaffected families `skipped`.
- When `.agent/temp/update-plan.md` has `Status: failed`, reset failed verification stages to `pending`. Reset Stage 0 first; after it passes, reset blocked later stages.
- Verdict status:
  - `pass`: no `❌` errors; minor `⚠️` warnings allowed.
  - `fail`: any `❌` error or critical `⚠️`.
  - `skipped`: terminal and excluded from all-pass checks.
  - `blocked`: non-terminal; final verdict fails until cleared.
- Classify identifiers used to locate/load code as primary (`❌`): class, interface, method, attribute, config key, path.
- Classify illustrative snippets or example paths as secondary (`⚠️`).
- Checkpoint `Last verified skill` mid-stage for context recovery.

---

## 1. Stage 0: Structural Integrity

Failure blocks all later stages; set them `blocked`.

- Skills/loaders: verify `.agent/skills/` directories and workflow loader listings are bidirectional. Missing directories or missing loader entries are `❌`.
- Missing profile extensions: allow only when recorded in the affected profile load-set table with exact status `⚠️ Missing profile reference`; exclude from loader union validation and report in Stage 0.
- Custom loaders: verify each `<custom>-*` prefix maps to `load-skills-<custom>.md`; uncategorized skills map only to `load-skills-custom.md`.
- Union: verify `load-skills-all.md` matches loaders in Standard Load Order, with custom prefix loaders sorted after Frontend and before generic custom.
- Workflow routes: verify `.agent/workflows/*.md` task routes match the `AGENTS.md` Workflows table and profile `Custom Workflow Routes`.
- Token estimates: compare summed skill file sizes / 4 to `Estimated context:`. If delta >15%, flag `⚠️` only.
- Cross-references: verify against the plan drift report.
- References: resolve `AGENTS.md` project paths, profile custom load-set entries, and `overview-skill-index` skill directories.
- Bootstrap freshness: for `all`, verify current-filesystem discovery was rebuilt and pre-execution staleness used reproducible Baseline Inputs Hash. Do not recompute the hash against post-execution files; valid `/update` runs intentionally edit material outputs.

---

## 2. Stage 1: Non-Project Asset Verification

Verify non-project config references:

1. Confirm referenced files exist.
2. Spot-check details such as framework versions and CI step names.
3. Flag stale references.

---

## 3. Stages 2-N: Per-Project Content Verification

For each project family, locate relevant skills by searching for `<FamilyPrefix>`.

1. Verify code identifiers:
   - Classes/interfaces exist.
   - File paths exist.
   - Namespaces and `using` directives exist.
   - Methods/properties exist.
   - Config keys match `appsettings*.json` or source.
   - Custom attributes and DI lifetimes exist.
2. Verify pattern accuracy against representative code.

---

## 4. Final Verdict

Aggregate executed stages:

| Stage statuses | Verdict | Action |
|---|---|---|
| All non-skipped stages `pass`, no warnings | Verified | Plan status -> `verified` |
| All non-skipped stages `pass`, warnings present | Verified with warnings | List warnings; plan status -> `verified` |
| Any `fail` or `blocked` | Failed | Consolidate corrections; plan status -> `failed` |
| Any `pending` or `executing` | Incomplete | Keep plan status -> `verifying` |

Success report:

```markdown
## Verification Complete
All non-skipped stages passed. Skill content is aligned.
Warnings, if any, include evidence, impact, invariant, recommendation, confidence, and verification status in `.agent/temp/update-verify-progress.md`.
Next steps:
1. Review `.agent/temp/update-verify-progress.md` for warnings.
2. Delete `.agent/temp/update-plan.md` and `.agent/temp/update-verify-progress.md` only if cleanup was requested.
3. Commit only if explicitly requested: `git add AGENTS.md .agent/ && git commit -m "chore(skills): sync agent configuration"`
```

Failure report:

```markdown
## Verification Failed: Corrections Required
Drift detected. Apply corrections and re-run `/update`.
| # | Stage | Skill/File | Evidence | Impact | Invariant | Recommendation | Confidence | Verification |
|---|---|---|---|---|---|---|---|---|
```

---

## 5. Design Properties

- Non-destructive and ephemeral: verification is read-only; cleanup requires request.
- Fail-fast: Stage 0 blocks downstream checks.
- Context-safe: checkpoint mid-stage for multi-window execution.
