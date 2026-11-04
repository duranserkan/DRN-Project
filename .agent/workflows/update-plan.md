---
description: Planning phase of /update — discover skills/projects, detect drift, generate update-plan.md
---

> **Sub-workflow of `/update`**. Receives `Scope`; default `all`.
> See also: [Status Lifecycle](./_shared/status-lifecycle.md) · [Operating Model](./_shared/workflow-operating-model.md)
> [!IMPORTANT]
> Flag drift, cross-references, stale references, and scope-widening only. Do not auto-modify them.
> **Estimated context: ~2.2K tokens**

## 1. Resolve Scope

If invoked directly, run the shared Startup Gate; otherwise inherit `/update` context.

- Receive the orchestrator's normalized `Requested Scope`, effective `Scope`, and new-run/resume decision.
- Generate one lowercase UUID v4 Run ID per new/superseding run. Preserve it during detailing/resumption; never reuse terminal/superseded plan IDs.
- Known scope (`all`, `skills`, `agents`, `projects`, `infra`, `<group>`, `<skill-dir>`, `files: <paths>`, `stage-<N>`): proceed.
- Omitted or `all`: `.agent/` may be copied. Rediscover from the current filesystem; stale profile facts indicate drift, not authority.
- Freeform: map to the nearest known scope/stages. Ask `Proceed / adjust / clarify?` and await approval. Then persist normalized `Requested Scope`, effective `Scope`, and `Resolved Stages`.

---

## 2. Discover Skills And Workflows

Build the skills manifest from `.agent/skills/*/SKILL.md` frontmatter. Record inclusion/exclusion reasons from destination evidence and profile `Excluded Skills`.

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

- Regenerate group loaders from included skills; preserve framework/profile conditions and append new entries at the end.
- Create one loader per custom prefix. Use `load-skills-custom.md` only for uncategorized skills or prefix collisions with portable/framework groups.
- Discover task workflows in `.agent/workflows/*.md` except `load-skills-*.md`, including meta/sub-workflows such as `documentation.md`, `commit-polish.md`, and `update*.md`.
- Sync only skill-loading or shared-workflow reference sections; preserve other workflow content.
- Classify every task workflow as portable, meta/sub-workflow, or repository-specific custom route.
- Record `Custom Workflows: <route> -> <workflow>` in the plan header as Stage 3's `Skill Discovery And Custom Routes` execution contract.
- Preserve prefix-mismatched inclusions; report new ones for confirmation.
- Keep `drn-testing` in group `drn`; keep `overview-drn-testing` in group `overview`.

---

## 3. Discover Projects, Assets, And Docs

Discover destination project/workspace manifests, package scripts, build files, and CI. Copied commands are not authority.

### Projects Manifest

Record project name, family prefix, layer, runnable, and test.

- Derive roles/layers from manifests, entry points, scripts, CI, and source; names are hints.

### Non-Project Assets

Record build/package config, lockfiles, editor files, CI/CD, containers, docs, and licenses. Include ecosystem manifests such as `package.json`, `pyproject.toml`, `Cargo.toml`, `go.mod`, or .NET project/solution files when present.

For `infra`, record exact discovered ecosystem manifest paths in the plan for executor scope dirt inspection. Include material non-output manifests in baseline inputs; declared outputs retain approved preimage guards. Discovery does not authorize edits outside resolved stages.

### Documentation Drift Pre-Scan

Per project README:

1. Check the first five lines; flag and skip `[STUB]` (`# Name` plus footer).
2. Extract README headers and code elements.
3. Extract public types and methods from project/source files.
4. Flag `[STALE]`, `[MISSING]`, or `[RENAMED]`.

For each `RELEASE-NOTES.md`, flag `[RELEASE-NOTE-TRIGGER]` when in-scope source or package metadata changes affect public contracts, configuration/defaults, security or operational behavior, data/migration behavior, observable fixes, or published package artifacts. Do not write release notes here; delegate to `/documentation`.

Limit: two tool calls per module.

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
| Project/workspace/package manifests and build scripts, including `*.slnx`, `*.sln`, `*.csproj`, `package.json`, `pyproject.toml`, `Cargo.toml`, `go.mod` | Stage 3, Stage 4, Stage 6 |
| `.github/workflows/**`, `Directory.*.props`, `global.json`, `nuget.config`, `Dockerfile*`, `docker-compose*` | Stage 4 |
| `README.md`, `docs/**/*.md`, `**/README.md`, `**/RELEASE-NOTES.md` | Stage 6 |
| Other existing file | Inspect owner and map nearest stage |
| Deleted or unknown file | Record drift; skip unless it names a removed skill/project/workflow |

Persist `Scope: files: <paths>` and `Resolved Stages: <stage list>`.

---

## 4. Diff And Report

Compare discovery with prior manifests.

- Skills: additions, removals, reclassifications, custom skills.
- Workflows: additions, removals, route renames, custom routes, stale `AGENTS.md` rows.
- Projects: missing, new, or renamed names.
- Stale references: old names, paths, or namespaces in sampled skill files.
- Portability: inspect in-scope agent links/read instructions for dependencies outside the copied bundle. Flag repository-local docs/source references in reusable skills/workflows even when valid here. Keep local source maps in the destination profile; use version-aware upstream framework references. Exclude `.agent/temp/` history.
- Preserve framework API names and serialized workflow identifiers. Labeled examples are not required destination paths/types.
- Flag `⚠️ Possibly irrelevant` skills when required folders/configs are absent. Propose exact names/reasons under profile `Excluded Skills`; require exclusion approval. Preserve directories; route needed Stage 3/profile outputs through scope-widening. Missing framework source alone does not establish irrelevance for package consumers.
- Sample at least one type, path, or config key per skill; flag stale items without auto-fixing.
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

- No plan or reconciled new run: run §2-§4 in scope; write affected stages as `pending` and others as `skipped`.
- `outlined` / `planning`: detail the next outlined stages to `pending`.
- All pending/skipped resolved: set status to `ready`.
- Persist Run ID, normalized Requested Scope, and effective Scope in the plan header. Before a Run ID or approval state changes, append the current approval state with reason and time to `### Approval History`, including the complete approval envelope when one exists; never edit or remove existing history. A new Run ID invalidates every prior verification-progress record, output manifest, current approval, and pass state even when paths or scopes match.
- Declare exact repository-relative Stage 1-5 `### Outputs` paths before review. Every mutating action must map to a declared output; use `N/A` only for non-mutating stages.
- Record `Baseline HEAD`; changed `HEAD` requires a fresh plan.
- Record `Baseline Inputs Hash` as the staleness gate per [`baseline-inputs-hash-spec.md`](./_shared/baseline-inputs-hash-spec.md).
- Persist the baseline status and checksum artifacts, and record `.agent/temp/update-baseline-inputs.manifest` as `Baseline Inputs Manifest`.
- Hash every material in-scope non-output input through the shared path-state contract. Declare one exact approved preimage-to-postimage transition for every Stage 1-5 output and coalesce repeated edits to the same path. A proposed diff is change evidence and never substitutes for an output preimage. For `all`, include the profile, discovered skill/workflow files, project/solution/package manifests, CI/container/build config files, and docs/source samples used for drift detection.
- Initialize `## Apply Approval` as required/pending. Review never grants apply approval.
- Use `N/A` only when no material non-output input paths exist; then record exactly `Baseline Inputs Hash Justification: no-material-input-files`, record `Baseline Inputs Manifest: N/A`, and ensure no baseline status or manifest artifact exists. Omit the justification otherwise.

Follow [`update.md` §5 Plan Contract](./update.md#5-plan-contract).
