---
description: Verification phase of /update — validate skill-body content against actual source code, staged per project family
---

> **Sub-workflow of `/update`** — invoked by the orchestrator after execution and review, never directly.
>
> Reads `Scope` from the plan header. Generates verification stages only for executed stages; skipped stages carry over as `skipped` in the progress file.

---

## 0. Initialize Progress File

Create `.agent/update-verify-progress.md` or resume from an existing one:

// turbo
```
view_file .agent/update-verify-progress.md
# Not found → create from template (§0.1)
# Found → resume from first non-done stage
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
> Status: pending | skipped | executing | pass | fail

### Checks
- [ ] Every `.agent/skills/` directory appears in its primary group workflow and vice-versa
- [ ] `all.md` = exact union of all group workflows, ordered: Basic → Overview → DRN Framework → Testing → Frontend → Custom
- [ ] Token estimates within 10% of actual file sizes ÷ 4
- [ ] Cross-references match detection report (no undocumented additions/removals)
- [ ] All `view_file` paths in workflows resolve to existing directories
- [ ] `AGENTS.md` project references resolve to actual projects
- [ ] `overview-skill-index/SKILL.md` references only existing skills

### Findings
_(populated during execution)_

---

## Stage 1: Non-Project Asset Verification
> Status: pending | skipped | executing | pass | fail

### Checks
- [ ] All non-project assets referenced in skills exist on disk
- [ ] Build config files (`Directory.Build.props`, `global.json`, etc.) match skill descriptions
- [ ] CI/CD workflow references in `overview-github-actions` resolve
- [ ] Docker-related references resolve (if applicable)
- [ ] No stale non-project file paths in skill content

### Findings
_(populated during execution)_

---

## Stages 2–N: Per-Project-Family Verification
> **Dynamic** — one stage per project-family prefix from the Projects Manifest.
> Scope-limited: generate only for affected families; unaffected → `skipped`.

#### Stage Generation Rule

For each distinct family prefix in the plan's Discovery Summary → Projects Manifest:
1. Title: `Stage <N>: <FamilyPrefix>.* Verification`
2. List actual projects under `> Projects:`
3. Apply per-family checks (see §3)

> [!IMPORTANT]
> Never hardcode families. DRN repos typically have `DRN.Framework.*`, `DRN.Nexus.*`, `Sample.*`, `DRN.Test.*`; business repos may have `Acme.Api.*`, `Acme.Core.*`. Generate only what exists.

#### Per-Family Stage Template

```markdown
## Stage N: <FamilyPrefix>.* Verification
> Status: pending | skipped | executing | pass | fail
> Projects: <list from manifest>
> Last verified skill: *(updated after each skill — enables mid-stage resumption)*

### Checks
- [ ] Skill-referenced classes/interfaces exist in source
- [ ] Skill-referenced file paths resolve
- [ ] Skill-referenced namespaces match actual
- [ ] Skill-documented patterns match current implementation
- [ ] Skill-referenced configuration keys/settings exist
- [ ] No stale code examples (removed/renamed APIs)

### Skills Checked
_(list each skill and result)_

### Findings
_(populated during execution)_
```

#### Examples

**DRN-Project repo (4 families → 4 stages)**

| Stage | Family | Projects |
|-------|--------|----------|
| 2 | `DRN.Framework.*` | SharedKernel, Utils, EntityFramework, Hosting, Testing, Jobs, MassTransit |
| 3 | `DRN.Nexus.*` | Application, Contract, Domain, Hosted, Infra, Utils |
| 4 | `Sample.*` | Application, Contract, Domain, Hosted, Infra, Utils |
| 5 | `DRN.Test.*` | Integration, Performance, Unit |

**Business repo (1 family → 1 stage)**

| Stage | Family | Projects |
|-------|--------|----------|
| 2 | `Acme.Api.*` | Domain, Application, Infra, Hosted |

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
````

---

## 1. Stage 0 — Structural Integrity

Gate check before deeper content verification — catches regressions introduced during review→commit.

### 1.1 Workflow ↔ Skill Directory Consistency

```
list_dir .agent/skills
# Each directory → verify presence in primary group workflow
# Each workflow skill entry → verify directory exists
```

### 1.2 `all.md` Union Validation

```
# Collect skills from group workflows only: basic.md, overview.md, drn.md, frontend.md, custom.md (if exists)
# Do NOT include task-workflow skill sections (review.md §2, update.md) — not part of all.md
# Compare collected skill list against all.md entries
# Verify section order: Basic → Overview → DRN Framework → Testing → Frontend → Custom
```

### 1.3 Token Estimate Verification

For each group workflow:
1. Sum file sizes of all referenced `SKILL.md` files
2. Divide by 4; compare with `Estimated context:` line
3. **Flag** if delta > 10%
4. Verify `all.md` total ≈ sum of group estimates

### 1.4 Cross-Reference Integrity

For each workflow, detect cross-references (skills whose directory prefix ≠ workflow group):
- Verify they match the plan's drift report
- Flag undocumented additions or removals

### 1.5 AGENTS.md & Skill Index

- `AGENTS.md` project references → resolve to actual `*.csproj` files
- `overview-skill-index/SKILL.md` → references only existing skill directories

### Completion

Check off items in progress file. Set Stage 0 to `pass` or `fail`.

> Stage 0 **failure blocks all subsequent stages** — structural integrity is prerequisite for content verification. Stop and report.
>
> Subsequent stages remain `pending` in the progress file. This is intentional — they are **inert** until Stage 0 is re-run and passes. Do not interpret `pending` stages as ready-to-proceed when Stage 0 is `fail`.

---

## 2. Stage 1 — Non-Project Asset Verification

Verify non-project file references across all skills remain accurate.

### 2.1 Identify Relevant Skills

Use the plan's non-project assets manifest as baseline. Scan for additional references in `.agent/skills/` (`SKILL.md` files only):

- `Directory.Build` / `Directory.Packages`
- `.github/workflows`
- `Dockerfile` / `docker-compose`
- `global.json` / `nuget.config` / `.editorconfig`

### 2.2 Verify References

For each reference:
1. Verify file exists on disk
2. If skill documents specific content (target framework version, CI step name) → spot-check against actual
3. Flag stale references

### Completion

Check off items. Set Stage 1 to `pass` or `fail`.

---

## 3. Stages 2–N — Per-Project Content Verification

For each project-family stage from §0.1, verify skills accurately describe current source.

### 3.1 Identify Relevant Skills

```
grep_search "<FamilyPrefix>" .agent/skills/ --include "SKILL.md"
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
1. Read pattern description from skill
2. Find representative implementation in source
3. Compare — flag divergence

### 3.4 Stage Completion

Set stage status:
- `pass` — no ❌, and any ⚠️ are **minor** (token estimate delta ≤10%, cosmetic wording, non-critical path reference)
- `fail` — any ❌, or ⚠️ on a critical reference (class name, method, namespace, config key, file path)

> **Primary reference** (escalate to ❌): the stale identifier is the *primary* way a consumer locates or uses the artifact — class name, interface, method, attribute, config key, or canonical file path. If not found in source, mark ❌.
>
> **Secondary reference** (keep as ⚠️): illustrative or supplementary — example file path, secondary usage snippet, slightly outdated prose that is not misleading.

> [!NOTE]
> **Mid-stage checkpointing**: Record individual skill results in the progress file as you go. If the context window is exhausted mid-stage, the next run can skip already-verified skills. If a stage exceeds the context window, mark `executing` and stop; next `/update` resumes via §0.

---

## 4. Final Stage — Verdict

After all stages complete (Stage 0 + Stage 1 + all per-family stages):

### 4.1 Aggregate & Determine Verdict

Update the summary table, then apply:

| Condition | Verdict | Action |
|-----------|---------|--------|
| All executed stages `pass`, no ⚠️ | ✅ **Verified** | Plan status → `verified` |
| All executed stages `pass`, some have ⚠️ warnings | ⚠️ **Verified with warnings** | List warnings, plan → `verified` |
| Any executed stage `fail` (contains ❌) | ❌ **Failed** | List failures, recommend re-run or manual fix |

> Skipped stages are excluded — they represent untouched, previously-verified state.

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
2. Commit: `git add .agent/ && git commit -m "chore(skills): sync agent configuration"`
3. Clean up: delete `.agent/update-verify-progress.md` and `.agent/update-plan.md`
```

> **Stage Resumption**: Follow the Stage Resumption Protocol from `update.md` §Plan File Contract, using `.agent/update-verify-progress.md` as coordination file. Terminal statuses: `pass`, `fail`, `skipped`.

---

## Design Properties

| Property | Guarantee |
|----------|-----------| 
| **Non-destructive** | Read-only — never modifies skill files or source |
| **Fail-fast** | Stage 0 failure blocks all subsequent stages |
| **Context-safe** | Each project stage fits one context window; `executing` persists for cross-window resumption |
| **Ephemeral** | `.agent/update-verify-progress.md` is temporary — delete after commit |