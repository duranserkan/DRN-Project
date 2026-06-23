---
description: Planning phase of /update — discover skills/projects, detect drift, generate update-plan.md
---

> **Sub-workflow of `/update`**. Receives `Scope`; default `all`.
> See also: [Status Lifecycle](./_shared/status-lifecycle.md) · [Operating Model](./_shared/workflow-operating-model.md)
> [!IMPORTANT]
> Flag drift, cross-references, stale references, and scope-widening only. Do not auto-modify them.
> **Estimated context: ~2.0K tokens**

## 1. Resolve Scope

If invoked directly, run the shared Startup Gate; otherwise inherit `/update` context.

- Known scope (`all`, `skills`, `agents`, `projects`, `infra`, `<group>`, `<skill-dir>`, `files: <paths>`, `stage-<N>`): proceed.
- Omitted or `all`: assume `.agent/` may be copied from another repository. Rebuild discovery from the current filesystem. Treat stale profile facts as drift evidence, not authority.
- Freeform scope: map it to the closest known scope and affected stages; ask `Proceed / adjust / clarify?`; stop until approved; then persist `Scope` and `Resolved Stages`.

---

## 2. Discover Skills And Workflows

Read `.agent/skills/*/SKILL.md` frontmatter and build the skills manifest.

| Prefix | Group | Loader |
|---|---|---|
| `basic-*` | basic | `load-skills-basic.md` |
| `overview-*` | overview | `load-skills-overview.md` |
| `drn-*` | drn | `load-skills-drn.md` |
| `test-*` | test | `load-skills-test.md` |
| `frontend-*` | frontend | `load-skills-frontend.md` |
| `<custom>-*` | custom group | `load-skills-<custom>.md` |
| other | custom | `load-skills-custom.md` |

Rules:

- Regenerate group loaders fully; append new entries at the end.
- Create one loader per custom prefix. Use `load-skills-custom.md` only for uncategorized skills or prefix collisions with portable/framework groups.
- Discover task workflows in `.agent/workflows/*.md` except `load-skills-*.md`, including meta/sub-workflows such as `documentation.md`, `commit-polish.md`, and `update*.md`.
- Sync only skill-loading or shared-workflow reference sections; preserve other workflow content.
- Classify every task workflow as portable, meta/sub-workflow, or repository-specific custom route.
- Record custom routes in the plan header as `Custom Workflows: <route> -> <workflow>`. The Stage 3 `Skill Discovery And Custom Routes` section must use that header as the execution contract.
- Preserve prefix-mismatched inclusions; report new ones for confirmation.
- Keep `drn-testing` in group `drn`; keep `overview-drn-testing` in group `overview`.

---

## 3. Discover Projects, Assets, And Docs

Scan the repository for solution files, project files, and assets.

### Projects Manifest

Record project name, family prefix, layer, runnable, and test.

- Hosted: `OutputType=Exe`, `Program.cs`, `launchSettings.json`, API/Web markers.
- Test: xUnit/NUnit, `*.Test(s)`, `*IntegrationTests`.
- Layer: Domain, Application, Infrastructure, Contract, Hosted, Utils.

### Non-Project Assets

Record build config, editor files, CI/CD, containers, docs, and root manifests: `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `nuget.config`, `.editorconfig`, `.gitattributes`, `.gitignore`, `.github/workflows/`, `Dockerfile*`, `docker-compose*`, `README.md`, `RELEASE-NOTES.md`, `LICENSE`.

### Documentation Drift Pre-Scan

For each project README:

1. Check the first five lines. If it is a stub (`# Name` plus footer), flag `[STUB]` and skip.
2. Extract README headers and code elements.
3. Extract public types and methods from project/source files.
4. Flag `[STALE]`, `[MISSING]`, or `[RENAMED]`.

For each `RELEASE-NOTES.md`, flag `[RELEASE-NOTE-TRIGGER]` when in-scope source or package metadata changes affect public contracts, configuration/defaults, security or operational behavior, data/migration behavior, observable fixes, or published package artifacts. Do not write release notes here; delegate to `/documentation`.

Budget: max two tool calls per module.

### Scope Filtering

| Scope | Skills/workflows | Projects/assets | Doc drift |
|---|---|---|---|
| `all` / omitted | All; ignore stale caches | Full | All |
| `skills` | All skills | Skip | Skip |
| `<group>` | `<group>-*` plus cross-reference validation | Skip | Skip |
| `<skill-dir>` | That skill plus parent group | Skip | Skip |
| `agents` / `projects` | Skip when cached manifest is current; rebuild needed slices otherwise | Full | Affected modules |
| `infra` | Skip | Non-project assets | Skip |
| `files: <paths>` | Path-derived subset | Path-derived subset | Path-derived subset |
| `stage-<N>` | Stage-scoped | Stage-scoped | Only Stage 6 |
| freeform | Resolved in §1 | Resolved in §1 | Resolved in §1 |

### File Scope Mapping

Normalize comma-separated `files:` paths relative to the repository root. Deduplicate and map:

| Path pattern | Stages |
|---|---|
| `.agent/skills/<skill-dir>/SKILL.md` | Parent group in Stage 1, Stage 2, Stage 5 |
| `.agent/workflows/load-skills-*.md` | Stage 1, Stage 2, Stage 5 |
| `.agent/workflows/*.md` task/meta/sub-workflows, including `documentation.md`, `commit-polish.md`, `test.md`, `update*.md` | Stage 1, Stage 3, Stage 5 |
| `.agent/workflows/_shared/*.md` | Stage 1, Stage 5 |
| `AGENTS.md`, `.agent/repository-profile.md` | Stage 3 |
| `*.slnx`, `*.sln`, `*.csproj` | Stage 3, Stage 6 |
| `.github/workflows/**`, `Directory.*.props`, `global.json`, `nuget.config`, `Dockerfile*`, `docker-compose*` | Stage 4 |
| `README.md`, `docs/**/*.md`, `**/README.md`, `**/RELEASE-NOTES.md` | Stage 6 |
| Other existing file | Inspect owner and map nearest stage |
| Deleted or unknown file | Record drift; skip unless it names a removed skill/project/workflow |

Persist `Scope: files: <paths>` and `Resolved Stages: <stage list>`.

---

## 4. Diff And Report

Compare discovery against previous manifests.

- Skills: additions, removals, reclassifications, custom skills.
- Workflows: additions, removals, route renames, custom routes, stale `AGENTS.md` rows.
- Projects: missing, new, or renamed names.
- Stale references: old names, paths, or namespaces in sampled skill files.
- Possibly irrelevant skills: flag `⚠️ Possibly irrelevant` when required folders/configs are absent; require approval before removal.
- Code reference sampling: check at least one type, path, or config key per skill; flag stale items and do not auto-fix.
- Cross-references and scope-widening: report wider impact and ask before widening.

Sync report template:

```markdown
## Sync Report
### Scope
- Requested: `<scope>` | Effective stages: <list> | Scope-widening: <none/details>
### Skills / Cross-References / Projects / Non-Project Assets / Documentation
- Evidence: <file:line or command output> | Impact: <risk> | Invariant: <rule> | Recommendation: <action> | Confidence: high/medium/low | Verification: run/not run/blocked/N/A
### Actions Planned
- [ ] <stage task>
```

Wait for affirmative confirmation (`yes`, `ok`, `confirmed`) before writing the plan.

---

## 5. Generate Plan File

Serialize the report and plan to `.agent/temp/update-plan.md`.

- No plan: run §2-§4 in scope; write affected stages as `pending` and others as `skipped`.
- `outlined` / `planning`: detail the next outlined stages to `pending`.
- All pending/skipped resolved: set status to `ready`.
- Record `Baseline HEAD` as audit metadata.
- Record `Baseline Inputs Hash` as the staleness gate per [`baseline-inputs-hash-spec.md`](./_shared/baseline-inputs-hash-spec.md).
- Hash every material in-scope input. For `all`, include `AGENTS.md`, profile, discovered skill/workflow files, project/solution/package manifests, CI/container/build config files, and docs/source samples used for drift detection.
- Use `N/A` only when no material input files exist, and then record exactly `Baseline Inputs Hash Justification: no-material-input-files`.

Follow the template in `update.md` §Plan File Contract.
