---
description: Execution phase of /update - read update-plan.md, execute sync stages
---

> **Sub-workflow of `/update`**. Reads reconciled scope and resumes approved stages.
> See also: [Status Lifecycle](./_shared/status-lifecycle.md) · [Operating Model](./_shared/workflow-operating-model.md)

## 0. Pre-Execution Validation

On direct invocation, route through `/update` for scope reconciliation and delegation. Otherwise inherit its context. Normal execution accepts only `plan-reviewed` or `executing`. Correction Mode alone accepts `failed` or `correcting`. Reject other states before mutation.

### Staleness Guard

1. Warn if plan age exceeds 24 hours.
2. Abort if `Baseline HEAD` differs from current `HEAD`. Revision changes require a fresh plan.
3. Require a non-empty Run ID and exact Requested Scope/effective Scope match across the invocation, plan, preview, and any correction progress.
4. Before the first execution mutation, recompute the Semantic Plan SHA-256, preview raw-byte SHA-256, optional proposed-diff SHA-256, and baseline manifest/hash. Abort unless those bindings match and the preview records every exact output preimage/postimage tuple, the current Run ID, requested/effective scope, and risk decision.
5. Require a complete explicit record whose subject/preview digests and shared approval-envelope digest match `.agent/temp/update-apply-preview.md`, the optional `.agent/temp/update-proposed.diff`, Run ID, requested/effective scope, producer, timestamp, and risk decision.
6. On resume, recompute the semantic-plan, preview, and optional diff hashes. Revalidate baseline `HEAD` and its complete shared path state. Classify outputs under Step 10.
7. Before the first mutation, abort if material non-output inputs exist and any baseline status or manifest artifact is missing, malformed, `N/A`, or fails revalidation.
8. Allow `Baseline Inputs Hash: N/A` only when `Baseline Inputs Manifest: N/A`, no baseline artifact exists, the plan contains exactly `Baseline Inputs Hash Justification: no-material-input-files`, and exact non-output input pathspecs still contain no material inputs.
9. Abort if any in-scope tracked or untracked change is not represented in the plan. Resolve and bytewise-deduplicate `<scope-paths>` from the table plus every declared Stage 1-5 output, for every scope. Use exact literal operands. If empty, record zero-dirt evidence and skip all three Git commands. Never pass bare `--`. Otherwise parse NUL records directly, validate single-link regular files without following links, and reject symlinks, hard links, or special files:

   ```bash
   GIT_CONFIG_NOSYSTEM=1 GIT_TERMINAL_PROMPT=0 GIT_OPTIONAL_LOCKS=0 git -c core.pager=cat -c diff.external= -c diff.noprefix=false status --porcelain=v1 -z --untracked-files=all -- <scope-paths>
   GIT_CONFIG_NOSYSTEM=1 GIT_TERMINAL_PROMPT=0 GIT_OPTIONAL_LOCKS=0 git -c core.pager=cat -c diff.external= -c diff.noprefix=false diff --no-ext-diff --no-textconv -- <scope-paths>
   GIT_CONFIG_NOSYSTEM=1 GIT_TERMINAL_PROMPT=0 GIT_OPTIONAL_LOCKS=0 git -c core.pager=cat -c diff.external= -c diff.noprefix=false diff --cached --no-ext-diff --no-textconv -- <scope-paths>
   ```

   Invoke commands separately. Require each to succeed before continuing. These flags disable system config, terminal prompts, optional locks, external diff, and textconv. They do not isolate local/global config, filters, credential helpers, fsmonitor, or network access. Require a trusted isolated environment that blocks those execution/network paths before running. Otherwise abort. Built-in command names cannot be replaced by Git aliases. Later success cannot mask failure.

   Scope dirt inspection never substitutes for the shared type/mode/content or approved-preimage guards.

10. Require exactly one approved preimage-to-postimage transition per output. Immediately before its first write, reproduce its exact preimage through the shared non-following path-state contract. On resume, pending plus exact preimage may write. Completed plus exact postimage continues. Pending plus exact postimage is checkpointed as applied only after exact verification. Every other combination invalidates approval and aborts. A proposed diff never substitutes for tuple comparison.

| Scope | `<scope-paths>` |
|---|---|
| `all` / omitted | `AGENTS.md`, `.agent/`, and every material input/output path in the plan |
| `<group>` | `.agent/workflows/load-skills-<group>.md`, `.agent/skills/<group>-*/**` |
| `<skill-dir>` | `.agent/skills/<skill-dir>/**` |
| `skills` | `.agent/skills/**`, all `load-skills-*.md`, task workflows |
| `agents` | `AGENTS.md`, profile, discovered manifests/build files, task/loader workflows, skill index |
| `projects` | discovered manifests/build files and project-referencing skills |
| `infra` | build config, CI/CD, Docker files, and exact discovered ecosystem manifest paths recorded in the plan |
| `files: <paths>` | exact listed paths |
| `stage-<N>` | files touched by Stage N |

Unrepresented dirt or a failed binding aborts as stale. Represented dirt must satisfy baseline and approved tuple checks.

### Correction Mode

`/update-execute` is the sole source-mutation owner for a failed run.

1. Enter only from `failed` or `correcting`. Verification progress must match the failed run's Run ID, requested/effective scope, Semantic Plan SHA-256, and failed Output Revision SHA-256.
2. Read stable finding IDs/targets from `Corrections Required`. Reject targets outside current scope or Stage 1-5 `### Outputs`. Scope/semantic-plan widening requires a fresh run.
3. Require `/update`'s correction-labeled apply preview, exact diff, and fresh explicit approval. Never reuse original apply approval.
4. Initialize or resume `.agent/temp/update-correction-progress.md`:

   ```markdown
   # Update Correction Progress
   > Run ID: <run-id> | Status: correcting | Correction Preview SHA-256: <sha256>
   > Requested Scope: <scope> | Scope: <effective scope> | Semantic Plan SHA-256: <sha256>
   ## Corrections
   - [ ] <finding-id> | <exact target> | <action>
   ```

5. Set `correcting` before corrections. Apply only pending approved items, checkpoint each, and abort on binding/target changes.
6. After all corrections, set correction progress and plan status to `done`. Require caller review before verification. Never transition directly to `reviewed` or `verifying`.

### Resumption And VCS

After all guards pass, persist `executing` before normal source mutation. Checkpoint stage progress.

Resume at the first incomplete stage. Git is read-only: no commit, push, stash, checkout, or ref mutation.

---

## 1. Stage 1: Sync Group Workflows

### Group Loaders

1. Preserve YAML frontmatter, descriptions, `// turbo` annotations, and skill order.
2. Regenerate included file-read entries. Preserve framework/profile conditions and append new skills at the end.
3. Retain token estimates only with a stated measurement basis. Otherwise omit them.
4. Create custom loaders under the [planner's group rules](./update-plan.md#2-discover-skills-and-workflows).
5. For an empty group, remove task/all-loader references. Report its loader for manual deletion unless deletion was approved.

### Task Workflows

Discover `.agent/workflows/*.md` task/meta/sub-workflows, excluding `load-skills-*.md`.

Use the plan's workflow classification and `Custom Workflows` header. An unplanned custom route or missing planned custom workflow makes the plan stale. Abort and return to `/update`.

Sync only skill-loading or shared-workflow reference sections. Preserve all other instructions.

### Custom Group And Irrelevance Removal

- Persist approved `Skill | Reason` rows under profile `Excluded Skills`. Omit excluded skills from loaders, task loading, and index routes. Retain directories. Preserve exclusions across runs. Changes require approval.

---

## 2. Stage 2: Sync `load-skills-all.md`

Regenerate from all group loaders:

1. Preserve YAML and `// turbo-all`.
2. Reference group loaders in Standard Load Order: Basic -> Overview -> Framework -> Testing -> Frontend -> Custom. Resolve framework groups from the profile. Do not duplicate skill lists. Preserve conditions and omit empty/excluded groups.
3. In Custom, list per-prefix custom loaders alphabetically, then `load-skills-custom.md` if present.
4. Include every custom loader with included skills. Load each skill once. Any estimate follows the group-loader measurement rule.

---

## 3. Stage 3: Sync `AGENTS.md` And Profile

Keep `AGENTS.md` portable. Put project-specific facts in `.agent/repository-profile.md`.

### Profile Overview And Commands

- Populate Type, Architecture, Frontend, and Testing from discovered projects.
- Replace copied commands using destination manifests, scripts, build files, or CI and its runner/package manager. Flag unsupported commands. Never invent them.
- Preserve the repo rule: do not run build/test commands unless the user explicitly allows it.

### Skill Discovery And Custom Routes

1. Read the plan header and discovery summary.
2. `Custom Workflows: <route> -> <workflow>` governs Stages 3/5.
3. Do not independently discover or reclassify a different custom route set during execution.
4. Use the plan classification for non-custom skill-loading, task, sub-workflow, and meta routes.
5. Update the `AGENTS.md` Workflows table from discovered task workflows. Add new routes, remove stale routes, and preserve portable descriptions when the route already exists.
6. Keep custom route details in the profile: route, workflow file, trigger/intent, and required custom loaders.
7. Create/refresh custom skill load-set entries in the profile.
8. Under affected profile load sets, record missing skills as `Skill | Expected Loader | Status | Resolution` with exact status `⚠️ Missing profile reference`. No placeholder rows. Exclude them from loaders/union checks, retaining profile entries. Restore when copied or remove/update entries after review. Owned by Stage 3, not Stage 6.

### Project Prefix Rename

If a project prefix changed:

1. Scan `.agent/repository-profile.md` and repository-owned overlay skills. Exclude generic/framework-scoped skills.
2. Present prefix mapping families for approval.
3. Apply boundary-aware regex:

   ```regex
   (?<=[ \t`'"\n\/]|^)<Prefix>\.
   ```

4. Verify every replaced path resolves.

---

## 4. Stage 4: Sync Non-Project References

Flag only. Verify command references to build config, CI/CD, containers, and manifests. Report stale items with the Evidence Contract. Do not modify files.

---

## 5. Stage 5: Sync Skill Index

Update `overview-skill-index/SKILL.md`:

- Set `last-updated` to today.
- Update task, layer, graph, and keyword routing.
- Add/remove custom task workflow routes, custom skill prefixes, and `new repository` / `port .agent` / `self-sync` routes.
- Verify every referenced skill directory exists.
- Verify every custom skill group routes through profile/custom overlay language.

---

## 6. Stage 6: Sync Project Docs

Flag only. Report stale, missing, or renamed README/skill content from plan data.

```markdown
## Stage 6: Project Docs Flags
### Content Drift / Skill Content Drift / Stale Project References
- Family.Module: Evidence: <file:line> | Impact: <risk> | Invariant: <rule> | Recommendation: <delegate/fix> | Confidence: high/medium/low | Verification: run/not run/blocked/N/A
```

Ask: `"Delegate updates to /documentation for each module? (Y/N)"`. If yes, load `documentation.md` with affected module scope and its preview gate.

---

## 7. Complete Plan

Set plan status to `done` only when every stage is terminal:

- In-scope stages are `done`.
- Out-of-scope stages are `skipped`.
- Any `pending`, `executing`, `blocked`, `fail`, or unresolved `Requires Approval` item blocks completion.
- Every actual Stage 1-5 source/configuration mutation matches an exact declared `### Outputs` path. Otherwise the plan is stale.

`update-verify.md` owns content verification.
