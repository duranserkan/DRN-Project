---
description: Planning phase of /update — discover skills/projects, detect drift, generate update-plan.md
---

> **Sub-workflow of `/update`** — called by the orchestrator, not invoked directly.
>
> Receives `Scope` from the orchestrator (default: `all`). Known scopes resolve immediately; freeform scopes are interpreted in §0 and confirmed before discovery begins.
>
> **Estimated context: ~3.0K tokens** (this workflow)

> [!IMPORTANT]
> **Safety Principle** — All drift items, cross-references, stale references, and scope-widening are **flagged for user decision only** — never auto-modified, auto-removed, or auto-widened.

---

## 0. Resolve Scope

If the scope matches a **known value** (`all`, `skills`, `agents`, `projects`, `infra`, `<group>`, `<skill-dir>`, `stage-<N>`), skip to §1.

If **freeform** (e.g., `login feature`, `update hosting skills`):

1. **Interpret & Resolve** — scan skills manifest (names, descriptions, keywords), project names, file paths, and workflow areas; map intent to closest known scope combination and affected stages
2. **Present** the resolution (input, interpretation, matched skills/projects, resolved stages) and ask: *Proceed / adjust / clarify?*
3. **Wait** for explicit approval (DiSCOS Confidence Signaling: Low); on rejection ask for clarification
4. **Persist** `Scope: <original freeform>` and `Resolved Stages: <concrete set>` in the plan header

All subsequent sections use the **resolved stage set**, not the raw freeform string.

---

## 1. Discover Skills

List every skill directory; read each `SKILL.md` frontmatter.

```
list_dir .agent/skills
# Per subdirectory: read SKILL.md → extract name, description, last-updated, difficulty
```

Build an in-memory **skills manifest**:

| Field | Source |
|-------|--------|
| `name` | YAML `name` |
| `description` | YAML `description` |
| `group` | Directory prefix (see classification) |
| `path` | `.agent/skills/<dir>/SKILL.md` |
| `tokens` | `bytes / 4` (approximate) |

### Group Classification

| Prefix | Group | Workflow |
|--------|-------|----------|
| `basic-*` | basic | `basic.md` |
| `overview-*` | overview | `overview.md` |
| `drn-*` | drn | `drn.md` |
| `test-*` | test | `load-skills-test.md` |
| `frontend-*` | frontend | `frontend.md` |
| *(no match)* | custom | `custom.md` |

### Workflow Types

| Type | Examples | Structure | Sync Rule |
|------|----------|-----------|-----------|
| **Group loader** | `basic.md`, `overview.md`, `drn.md`, `frontend.md`, `test.md` | `view_file` skill lists + YAML frontmatter + token estimate | §4 template regeneration applies fully |
| **Task workflow** | `test.md`, `review.md`, `update.md` | Procedural steps with embedded `view_file` lines | Sync **only** `view_file` entries in the skill-loading section; preserve all surrounding prose |

### Cross-Group Inclusions

Skills whose directory prefix ≠ the workflow's primary group are **cross-references**.

**Rules:** detect by comparing prefix against workflow group → preserve existing ones unconditionally → report newly detected ones for user confirmation → never auto-add.

> [!NOTE]
> `drn-testing` (group `drn`) and `overview-drn-testing` (group `overview`) are distinct skills. Do not confuse them during drift detection.

---

## 2. Discover Projects

Scan the repository for project structure:

```
find_by_name *.slnx  (or *.sln)
grep_search for <Project entries in the solution file  # Or: find_by_name *.csproj

# Classify by signals (priority order):
#   Hosted/runnable: <OutputType>Exe</OutputType> | Program.cs | launchSettings.json
#   Test: xUnit/NUnit/MSTest packages | name matches *.Test.* / *.Tests / *IntegrationTests
#   Layer: Domain, Application, Infrastructure/Infra, Contract, Hosted/Api/Web, Utils
```

> [!NOTE]
> Business repos may use different conventions (e.g., `*.Api` for hosted, `*.Tests` plural). Always prefer `.csproj` content analysis over name-only heuristics.

Build a **projects manifest**: project name, family prefix, layer, runnable (bool), test (bool).

### 2.1 Discover Non-Project Assets

Scan for solution-level and repo-level files that skills reference but are not `.csproj` projects:

```
# Build infrastructure
find_by_name Directory.Build.props / Directory.Packages.props / global.json / nuget.config

# Editor / repo config
find_by_name .editorconfig / .gitattributes / .gitignore

# CI/CD
list_dir .github/workflows   (if exists)
find_by_name Dockerfile* / docker-compose*

# Documentation
find_by_name README.md / RELEASE-NOTES.md / LICENSE
```

Build a **non-project assets manifest** (file, category, exists ✅/❌). Categories: Build config, Editor, CI/CD, Container, Docs.

> [!IMPORTANT]
> Non-project assets are critical infrastructure in business repos (central package management, build props, CI pipelines). Skills referencing these need validation coverage equal to project references.

### Scope Filtering (§1 & §2)

| Scope | §1 Skills | §2 Projects & Assets |
|-------|-----------|----------------------|
| `all` / *(omitted)* | All skills | Full discovery |
| `skills` | All skills | **Skip §2** |
| `<group>` (e.g. `basic`) | `<group>-*` only — build full manifest for cross-ref validation | **Skip §2** |
| `<skill-dir>` (e.g. `drn-hosting`) | That skill only — identify parent group | **Skip §2** |
| `agents` / `projects` | **Skip §1** (use cached manifest) | Full discovery |
| `infra` | **Skip §1** | Non-project assets only (§2.1) |
| `stage-<N>` | Only if stage N needs it | Only if stage N needs it (stages 3, 4) |
| *(freeform)* | Per §0 resolved set | Per §0 resolved set |

---

## 3. Diff & Report

Compare discovered state against existing manifests.

### 3.1–3.3 Drift Detection

| Drift Type | How |
|------------|-----|
| **Skill added** | Directory exists, not listed in workflow |
| **Skill removed** | Listed in workflow, directory missing |
| **Skill reclassified** | Prefix changed (rare) |
| **Custom skill** | No standard prefix → flag as `custom`; check if `custom.md` exists |
| **Project drift** | Compare `AGENTS.md` project references against discovered projects — flag renamed, missing, new |

### 3.4 Possibly Irrelevant Skills

Extract each skill's referenced project families and non-project assets. Flag as `⚠️ Possibly irrelevant` when **none** resolve:

> Examples (non-normative — apply the general rule below for all skills):

| Condition | Flagged Skills |
|-----------|----------------|
| No frontend/Razor/Vite infrastructure | `frontend-*` |
| No hosted/runnable project | `drn-hosting` |
| No test infrastructure | `test-*`, `drn-testing` |
| No CI/CD workflows directory | `overview-github-actions` |
| No NuGet packaging config | NuGet-publish skills |
| Referenced project family entirely absent | Any skill referencing it |

> General rule: **extract references → check existence → flag if none resolve**. Scales to any repo without hardcoding.

### 3.5 Skill-Body ↔ Source-Code Drift (Sampling)

Spot-check a representative subset of each skill's code references (backtick-quoted identifiers, file paths, namespaces, config keys) via `grep_search` / `find_by_name`:

- **Sampling**: ≥1 reference per type (class, file path, namespace, config key), or all if ≤ 5 total
- **Flag** stale references as `⚠️ Stale reference in <skill>: <identifier> not found`
- **Do not auto-fix** — flag for user decision only

> [!NOTE]
> Detects drift that project-name substitution cannot (renamed methods, deleted files, changed config keys). Exhaustive verification deferred to `update-verify.md` §3.

### 3.6 Cross-Reference & Scope-Widening Validation

**Cross-references** — per workflow, detect prefix ≠ group entries and compare against previous sync. Flag new or removed cross-references for user confirmation.

**Scope-widening** (scoped runs only; `all` skips this):
1. **Scan** cross-references in the affected group's workflow — identify skills from other groups referencing the changed skill(s)
2. **Check** whether the change would stale those cross-references
3. **Report** wider impact per `update.md` §Scope-Widening Rule — list additional affected stages/groups, ask whether to widen or defer, never auto-widen

### 3.7 Report

Present summary to user:

```markdown
## Sync Report

### Scope
- **Requested**: `<scope value>`
- **Effective stages**: 1, 2, 5 *(skipping: 3, 4)*
- **Scope-widening**: None / ⚠️ <details>

### Skills
- ✅ <N> in sync | ➕ Added: <list> | ➖ Removed: <list>
- 🔄 Reclassified: <list> | 🆕 Custom: <list>
- ⚠️ Possibly irrelevant: <list with reason>
- ⚠️ Stale code references: <list with skill + identifier>

### Cross-References
- ✅ <N> unchanged | 🆕 New: <list> | ➖ Removed: <list>

### Projects
- ✅ <N> in sync | 🔄 Changed: <list old → new>
- 🔀 Prefix mapping: <old> → <new> (for skill body substitution)

### Non-Project Assets
- ✅ <N> found | ❌ Missing: <list referenced but absent>
- ℹ️ Categories: Build config / CI-CD / Container / Docs

### Actions Planned
- [ ] Update <workflow>.md / all.md / AGENTS.md / overview-skill-index/SKILL.md
- [ ] Sync non-project asset references
- [ ] Substitute project names in skill bodies (if prefix changed)
- [ ] Create custom.md (if needed)
- [ ] Flag skills with stale code references for regeneration
```

Wait for explicit user confirmation (DiSCOS Autonomy Ladder level 3). Accept any **clear affirmative intent** — e.g., `proceed`, `ok`, `yes`, `go ahead`, `looks good`, `confirmed`, `approved`, or equivalent phrasing (case-insensitive). A question, partial qualifier (`"yes but…"`), ambiguous statement (`"maybe"`), or silence is **not** confirmation — re-prompt once before writing the plan file.

> [!NOTE]
> A response that both confirms **and** modifies scope (e.g., *"yes but skip Stage 4"*) is treated as a scope-change request — acknowledge the change, update the planned actions, and re-present the updated report for a clean confirmation before writing the plan file.

> [!IMPORTANT]
> **Two gates, two purposes — both are mandatory:**
> - **Gate 1 (Preview)**: This acknowledgment confirms the user has seen the drift report and agrees with the planned actions *before* the plan file is written.
> - **Gate 2 (Formal approval)**: After the plan reaches `ready` status, the orchestrator (`update.md §2.2`) calls `review.md` on the plan file. Execution does not begin until this review passes.
>
> Do not conflate the two — skipping either gate bypasses a required safety check.

---

## 4. Generate Plan File

After discovery and diff, serialize results to `.agent/update-plan.md`.

### Behavior by State

| Current State | Action |
|---------------|--------|
| No plan file | Run §1–§3 (within scope), write plan: affected → `pending`, others → `skipped` |
| `outlined` | Detail next `outlined` stage → `pending` (skip if out of scope) |
| `planning` | Continue detailing → `pending` |
| All `pending`/`skipped` | Set overall to `ready`, stop |
| `ready` | Report "Plan already complete", stop |

Write `Scope:` and `Resolved Stages:` in the plan header. Affected stages → `pending`; unaffected → `skipped` with `_(skipped — out of scope)_`.

### Staged Planning

Single-pass is the common path. For very large repos where §3.5 exhausts context budget, write stages as `outlined` initially; subsequent `/update` calls detail them to `pending` until all stages are `pending`/`skipped`.

### Plan File Template

Write to `.agent/update-plan.md` following the template and design rules in `update.md` §Plan File Contract.

<!-- Example (non-normative — shows a well-formed stage):
## Stage 1: Sync Group Workflows
> Status: pending
> Maps to: §1.1, §1.2, §1.3

### Actions
- [ ] Update `basic.md` — add basic-new-skill, remove basic-old-skill
- [ ] Update `drn.md` — update token estimates
-->
