---
description: Planning phase of /update — discover skills/projects, detect drift, generate update-plan.md
---

> **Sub-workflow of `/update`** — receives `Scope` (default: `all`).
> [!IMPORTANT]
> **Safety**: Drift, cross-references, stale references, and scope-widening are **flagged only** — never auto-modified.
> **Estimated context: ~1.5K tokens**

---

## 1. Resolve Scope
- **Known scopes** (`all`, `skills`, `agents`, `projects`, `infra`, `<group>`, `<skill-dir>`, `files: <paths>`, `stage-<N>`): Proceed to §2.
- **Freeform scope**:
  1. **Interpret**: Scan skills manifest, projects, and files to map to closest known scope + affected stages.
  2. **Present**: Ask user: *Proceed / adjust / clarify?*
  3. **Wait**: Stop and await approval.
  4. **Persist**: Write `Scope` and `Resolved Stages` to the plan header.

---

## 2. Discover Skills
List directories in `.agent/skills/` and read each `SKILL.md` frontmatter. Build the **skills manifest** in-memory:

| Prefix | Group | Workflow |
|--------|-------|----------|
| `basic-*` | basic | `load-skills-basic.md` |
| `overview-*` | overview | `load-skills-overview.md` |
| `drn-*` | drn | `load-skills-drn.md` |
| `test-*` | test | `load-skills-test.md` |
| `frontend-*` | frontend | `load-skills-frontend.md` |
| `<custom>-*` | custom group | `load-skills-<custom>.md` |
| *(other)* | custom | `load-skills-custom.md` |

- **Group loaders**: Regenerated fully.
- **Task workflows**: Sync `view_file` listings in skill-loading sections; preserve other content.
- **Cross-references**: Preserve prefix-mismatched inclusions; report new ones for confirmation.
- *Note*: Do not confuse `drn-testing` (group `drn`) with `overview-drn-testing` (group `overview`).

---

## 3. Discover Projects & Assets
Scan repository for solution files, projects, and assets.

### 3.1 Projects Manifest
Detect projects and build manifest: name, family prefix, layer, runnable (bool), test (bool).
- **Hosted**: OutputType=Exe, Program.cs, launchSettings.json, API/Web.
- **Test**: xUnit/NUnit, `*.Test(s)`, `*IntegrationTests`.
- **Layer**: Domain, Application, Infrastructure, Contract, Hosted, Utils.

### 3.2 Non-Project Assets
Discover non-project config files:
- *Build*: `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `nuget.config`.
- *Editor*: `.editorconfig`, `.gitattributes`, `.gitignore`.
- *CI/CD*: `.github/workflows/`.
- *Container*: `Dockerfile*`, `docker-compose*`.
- *Docs*: `README.md`, `RELEASE-NOTES.md`, `LICENSE`.

### 3.3 Documentation Drift Pre-Scan
For projects with a `README.md`:
1. Check first 5 lines. If stub (`# Name` + footer), flag `[STUB]` and skip.
2. Extract README headers and code elements (types, methods, config keys).
3. Extract actual public types/methods from `.csproj` / source.
4. Flag divergence: `[STALE]` (stale reference in README), `[MISSING]` (missing reference in README), `[RENAMED]` (mismatch).
*Budget*: Max 2 tool calls per module.

### 3.4 Scope Filtering
| Scope | §2 Skills | §3.1-3.2 Projects & Assets | §3.3 Doc Drift |
|-------|-----------|----------------------------|----------------|
| `all` / *(omitted)* | All skills | Full | All |
| `skills` | All skills | Skip | Skip |
| `<group>` (e.g. `basic`) | `<group>-*` only; build manifest for cross-reference validation | Skip | Skip |
| `<skill-dir>` (e.g. `drn-hosting`) | That skill only; identify parent group | Skip | Skip |
| `agents` / `projects` | Skip when cached manifest is current; otherwise rebuild needed manifest slices | Full | Affected modules |
| `infra` | Skip | Non-project assets only | Skip |
| `files: <paths>` | Path-derived subset | Path-derived subset | Path-derived subset |
| `stage-<N>` | Stage-scoped | Stage-scoped | Only if stage 6 |
| *(freeform)* | Per §1 resolved set | Per §1 resolved set | Per §1 resolved set |

---

### 3.5 File Scope Mapping
`files:` scopes are deterministic, not freeform. Split comma-separated paths, normalize relative to the repository root, deduplicate, and map to stages:

| Path Pattern | Stages |
|--------------|--------|
| `.agent/skills/<skill-dir>/SKILL.md` | Parent group in Stage 1, Stage 2, Stage 5 |
| `.agent/workflows/load-skills-*.md`, `.agent/workflows/test.md`, `.agent/workflows/review.md`, `.agent/workflows/develop.md` | Stage 1, Stage 2, Stage 5 |
| `AGENTS.md`, `.agent/repository-profile.md` | Stage 3 |
| `*.slnx`, `*.sln`, `*.csproj` | Stage 3, Stage 6 |
| `.github/workflows/**`, `Directory.*.props`, `global.json`, `nuget.config`, `Dockerfile*`, `docker-compose*` | Stage 4 |
| `README.md`, `docs/**/*.md`, `**/README.md`, `**/RELEASE-NOTES.md` | Stage 6 |
| Other existing file | Inspect owner and map to nearest affected stage |
| Deleted or unknown file | Record in drift report and skip unless it names a removed skill/project/workflow |

Persist `Scope: files: <paths>` and `Resolved Stages: <stage list>` in the plan header.

---

## 4. Diff & Report
Compare discovered assets against previous manifests to detect drift.

### 4.1 Drift Detection
- **Skills**: Directory additions, removals, reclassifications, or custom skills.
- **Projects**: Missing, new, or renamed project names.
- **Stale references**: Old names, paths, or namespaces inside skill files (sampling).

### 4.2 Possibly Irrelevant Skills
Flag `⚠️ Possibly irrelevant` when referenced folders/configs are absent:
- No frontend files → `frontend-*`.
- No runnable projects → `drn-hosting`.
- No tests → `test-*`, `drn-testing`.
- No workflows → `overview-github-actions`.
*Note*: Removals of flagged irrelevant skills require explicit user approval.

### 4.3 Code Reference Sampling
Check $\ge 1$ code reference (types, paths, config keys) per skill via `grep_search`. Flag stale items: `⚠️ Stale reference in <skill>: <id> not found`. Do not auto-fix.

### 4.4 Cross-References & Scope-Widening
Detect cross-references and check if scoped changes affect other groups. Report wider impact and ask user (never auto-widen).

### 4.5 Sync Report Template
```markdown
## Sync Report
### Scope
- **Requested**: `<scope>` | **Effective stages**: <list> | **Scope-widening**: <none/details>
### Skills / Cross-References / Projects / Non-Project Assets / Documentation
- [Details of drift, stubs, stale references]
### Actions Planned
- [ ] List of planned stage tasks
```
Wait for user confirmation (affirmative like `yes`, `ok`, `confirmed`).

---

## 5. Generate Plan File
Serialize report and plan to `.agent/temp/update-plan.md`.
- **No plan file**: Run §2-§4 (within scope); write plan with affected as `pending`, others as `skipped`.
- **`outlined` / `planning`**: Detail next outlined stages to `pending`.
- **All pending/skipped resolved**: Set overall status to `ready`.

Follow the template in `update.md §Plan File Contract`.
