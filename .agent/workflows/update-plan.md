---
description: Planning phase of /update — discover skills/projects, detect drift, generate update-plan.md
---

> **Sub-workflow of `/update`** — receives `Scope` from orchestrator (default: `all`).

> [!IMPORTANT]
> **Safety** — Drift, cross-references, stale references, and scope-widening are **flagged only** — never auto-modified.

---

## 0. Resolve Scope

Known values (`all`, `skills`, `agents`, `projects`, `infra`, `<group>`, `<skill-dir>`, `stage-<N>`) → skip to §1.

**Freeform** (e.g., `login feature`, `update hosting skills`):

1. **Interpret** — scan skills manifest, project names, file paths; map to closest known scope + affected stages
2. **Present** resolution — ask: *Proceed / adjust / clarify?*
3. **Wait** for explicit approval (DiSCOS Confidence: Low)
4. **Persist** `Scope: <original>` and `Resolved Stages: <concrete set>` in plan header

Subsequent sections use the **resolved stage set**.

---

## 1. Discover Skills

List skill directories; read each `SKILL.md` frontmatter.

```
list_dir .agent/skills
# Per subdirectory: read SKILL.md → extract name, description, last-updated, difficulty
```

Build in-memory **skills manifest**:

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
| `<custom-prefix>-*` | *per mapping* | `load-skills-<custom-prefix>.md` |
| *(no match)* | custom | `custom.md` |

> **Custom prefix→group**: Read from `Custom Groups:` in existing `update-plan.md` header (resume) or from `update.md` §Plan File Contract (first run). Planner creates the group loader if absent. Matching skills classify into that group instead of `custom`.

### Workflow Types

| Type | Sync Rule |
|------|-----------|
| **Group loader** (`load-skills-*.md`) | Regenerate full §4 template |
| **Task workflow** (`test.md`, `review.md`, `update.md`) | Sync `view_file` entries in skill-loading section only; preserve surrounding prose |

### Cross-Group Inclusions

Skills with directory prefix ≠ the workflow's primary group are **cross-references**:

- Preserve existing cross-references unconditionally
- Report new ones for user confirmation — never auto-add

> [!NOTE]
> `drn-testing` (group `drn`) and `overview-drn-testing` (group `overview`) are distinct skills. Do not confuse during drift detection.

---

## 2. Discover Projects

Scan repository for projects:

```
find_by_name *.slnx  (or *.sln)
grep_search <Project entries in solution file  # Or: find_by_name *.csproj

# Classify by signals (priority order):
#   Hosted/runnable: <OutputType>Exe</OutputType> | Program.cs | launchSettings.json
#   Test: xUnit/NUnit/MSTest packages | *.Test.* / *.Tests / *IntegrationTests
#   Layer: Domain, Application, Infrastructure/Infra, Contract, Hosted/Api/Web, Utils
```

> [!NOTE]
> Business repos may use different conventions (e.g., `*.Api` for hosted). Prefer `.csproj` content over name-only heuristics.

Build **projects manifest**: name, family prefix, layer, runnable (bool), test (bool).

### 2.1 Discover Non-Project Assets

Scan for solution-level and repo-level files not covered by `.csproj` projects:

```
find_by_name Directory.Build.props / Directory.Packages.props / global.json / nuget.config  # Build
find_by_name .editorconfig / .gitattributes / .gitignore                                    # Editor
list_dir .github/workflows   (if exists)                                                    # CI/CD
find_by_name Dockerfile* / docker-compose*                                                  # Container
find_by_name README.md / RELEASE-NOTES.md / LICENSE                                         # Docs
```

Build **non-project assets manifest** (file, category, exists ✅/❌). Categories: Build config, Editor, CI/CD, Container, Docs.

> [!IMPORTANT]
> Non-project assets need validation coverage equal to project references.

### 2.2 Documentation Drift Pre-Scan

For each project with a `README.md`:

1. **Read** first 5 lines → classify **Rich** (has content) or **Stub** (`# Name` + footer only → flag `[STUB]`, skip)
2. **Extract** README `##`/`###` headings; note types, methods, config keys
3. **Extract** source elements via `view_file_outline <Module>/<Module>.csproj` → public types, key methods
4. **Compare** — flag divergence:
   - `[STALE]` — README references type/method absent from source
   - `[MISSING]` — source public type absent from README
   - `[RENAMED]` — heading/type name mismatch suggesting rename
5. **Record** in `### Documentation Drift` table in Discovery Summary

> **Budget**: ~2 tool calls per module. Total ≤ 2 × |Projects Manifest|.

### Scope Filtering (§1, §2, §2.2)

| Scope | §1 Skills | §2 Projects & Assets | §2.2 Doc Drift |
|-------|-----------|----------------------|----------------|
| `all` / *(omitted)* | All skills | Full discovery | All modules |
| `skills` | All skills | **Skip §2** | **Skip** |
| `<group>` (e.g. `basic`) | `<group>-*` only — build full manifest for cross-ref validation | **Skip §2** | **Skip** |
| `<skill-dir>` (e.g. `drn-hosting`) | That skill only — identify parent group | **Skip §2** | **Skip** |
| `agents` / `projects` | **Skip §1** (use cached manifest) | Full discovery | Affected modules |
| `infra` | **Skip §1** | Non-project assets only (§2.1) | **Skip** |
| `stage-<N>` | Only if stage N needs it | Only if stage N needs it (stages 3, 4) | Only if stage 6 |
| *(freeform)* | Per §0 resolved set | Per §0 resolved set | Per §0 resolved set |

---

## 3. Diff & Report

Compare discovered against existing manifests.

### 3.1–3.3 Drift Detection

| Drift Type | Detection |
|------------|-----------|
| **Skill added** | Directory exists, not in workflow |
| **Skill removed** | In workflow, directory missing |
| **Skill reclassified** | Prefix changed (rare) |
| **Custom skill** | No standard prefix → flag `custom`; check `custom.md` exists |
| **Project drift** | `AGENTS.md` references vs discovered projects — flag renamed, missing, new |

### 3.4 Possibly Irrelevant Skills

Extract each skill's referenced project families and non-project assets. Flag `⚠️ Possibly irrelevant` when **none** resolve:

| Condition | Flagged Skills |
|-----------|----------------|
| No frontend/Razor/Vite infrastructure | `frontend-*` |
| No hosted/runnable project | `drn-hosting` |
| No test infrastructure | `test-*`, `drn-testing` |
| No CI/CD workflows directory | `overview-github-actions` |
| No NuGet packaging config | NuGet-publish skills |
| Referenced project family absent | Any skill referencing it |

> **General rule**: Extract references → check existence → flag if none resolve.

#### On User-Approved Removal

On approval, execute removal per `update-execute.md` §1.4. Run §3.6 validation before committing removals to catch dangling refs.

### 3.5 Skill-Body ↔ Source-Code Drift (Sampling)

Spot-check representative code references (backtick-quoted identifiers, file paths, namespaces, config keys) via `grep_search` / `find_by_name`:

- **Sampling**: ≥1 reference per type (class, file path, namespace, config key), or all if ≤5 total
- **Flag** stale references: `⚠️ Stale reference in <skill>: <identifier> not found`
- **Do not auto-fix** — flag for user decision only

> [!NOTE]
> Detects drift that project-name substitution cannot (renamed methods, deleted files, changed config keys). Exhaustive verification deferred to `update-verify.md` §3. Module-associated skill drift flagged in Stage 6 and can trigger `/documentation` delegation.

### 3.6 Cross-Reference & Scope-Widening Validation

**Cross-references** — per workflow, detect prefix ≠ group entries; compare against previous sync. Flag changes for user confirmation.

**Scope-widening** (scoped runs only; `all` skips):
1. Scan affected group's cross-references for skills referencing changed skill(s)
2. Check whether the change would stale those references
3. Report wider impact per `update.md` §Scope-Widening Rule — list affected stages/groups, ask to widen or defer (never auto-widen)

### 3.7 Report

Present to user using these sections (omit sections with no findings):

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

### Documentation
- ✅ <N> modules in sync | ⚠️ Drift detected: <list with STALE/MISSING/RENAMED counts>
- 📄 Stub modules (deferred): <list>

### Actions Planned
- [ ] Update <workflow>.md / all.md / AGENTS.md / overview-skill-index/SKILL.md
- [ ] Sync non-project asset references
- [ ] Substitute project names in skill bodies (if prefix changed)
- [ ] Create custom.md (if needed)
- [ ] Flag skills with stale code references for regeneration
- [ ] Remove user-approved irrelevant skills from group loaders / all.md / AGENTS.md / skill index (if any)
- [ ] Flag documentation drift for <N> modules (delegate to `/documentation` on request)
- [ ] Flag skill content drift for <N> modules (delegate to `/documentation` on request)
```

Wait for explicit user confirmation (DiSCOS Autonomy Ladder level 3). Accept clear affirmative (case-insensitive: `proceed`, `ok`, `yes`, `go ahead`, `looks good`, `confirmed`, `approved`). Ambiguous/qualified → re-prompt once.

> [!IMPORTANT]
> **Two mandatory gates:**
> - **Gate 1 (Preview)** — user confirms drift report and planned actions before plan file is written.
> - **Gate 2 (Formal approval)** — after plan reaches `ready`, orchestrator (`update.md §2.2`) calls `review.md` before execution.
>
> Confirm-and-modify response (e.g., *"yes but skip Stage 4"*) is a scope-change — update actions and re-present for confirmation.

---

## 4. Generate Plan File

Serialize results to `.agent/update-plan.md`.

### Behavior by State

| Current State | Action |
|---------------|--------|
| No plan file | Run §1–§3 (within scope), write plan: affected → `pending`, others → `skipped` |
| `outlined` | Detail next `outlined` stage → `pending` (skip if out of scope) |
| `planning` | Continue detailing → `pending` |
| All `pending`/`skipped` | Set overall to `ready`, stop |
| `ready` | Report "Plan already complete", stop |

Write `Scope:` and `Resolved Stages:` in plan header. Affected → `pending`; unaffected → `skipped` with `_(skipped — out of scope)_`.

### Staged Planning

Default: single-pass. If §3.5 exhausts context budget, write stages as `outlined`; subsequent `/update` calls detail them to `pending`.

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
