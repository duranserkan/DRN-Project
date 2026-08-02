---
description: Execution phase of /update — read update-plan.md, execute sync stages
---

> **Sub-workflow of `/update`**. Not invoked directly. Reads `Scope` and follows Stage Resumption Protocol.
> See also: [Status Lifecycle](./_shared/status-lifecycle.md) · [Operating Model](./_shared/workflow-operating-model.md)
> **Estimated context: ~2.5K tokens**

## 0. Pre-Execution Validation

If invoked directly, run the shared Startup Gate; otherwise inherit `/update` context.

### Staleness Guard

1. Warn if the plan is older than 24 hours.
2. Abort if `Baseline HEAD` differs from current `HEAD`; revision changes require a fresh plan.
3. Require a non-empty Run ID and exact Requested Scope/effective Scope match across the invocation, plan, preview, and any correction progress.
4. Before the first execution mutation, recompute the Semantic Plan SHA-256, preview raw-byte SHA-256, optional proposed-diff SHA-256, and baseline manifest/hash. Abort unless those bindings match and the preview records every exact output preimage/postimage tuple, the current Run ID, requested/effective scope, and risk decision.
5. Require a complete explicit record whose subject/preview digests and shared approval-envelope digest match `.agent/temp/update-apply-preview.md`, the optional `.agent/temp/update-proposed.diff`, Run ID, requested/effective scope, producer, timestamp, and risk decision.
6. On resume, recompute the semantic-plan, preview, and optional diff hashes. Revalidate baseline `HEAD` and its complete shared path state. Classify every output only by its approved tuple: exact preimage is pending; exact postimage is completed after verification; any other state aborts.
7. Before the first mutation, abort if material non-output inputs exist and any baseline status or manifest artifact is missing, malformed, `N/A`, or fails revalidation.
8. Allow `Baseline Inputs Hash: N/A` only when `Baseline Inputs Manifest: N/A`, no baseline artifact exists, the plan contains exactly `Baseline Inputs Hash Justification: no-material-input-files`, and exact non-output input pathspecs still contain no material inputs.
9. Abort if any in-scope tracked or untracked change is not represented in the plan. Resolve and bytewise-deduplicate the exact literal `<scope-paths>` operands first. If that set is empty, record explicit zero-dirt evidence and skip all three Git commands; never pass bare `--`. Otherwise parse NUL records directly, validate single-link regular files without following links, and reject symlinks, hard links, or special files:

   ```bash
   GIT_CONFIG_NOSYSTEM=1 GIT_TERMINAL_PROMPT=0 GIT_OPTIONAL_LOCKS=0 git -c core.pager=cat -c diff.external= -c diff.noprefix=false status --porcelain=v1 -z --untracked-files=all -- <scope-paths>
   GIT_CONFIG_NOSYSTEM=1 GIT_TERMINAL_PROMPT=0 GIT_OPTIONAL_LOCKS=0 git -c core.pager=cat -c diff.external= -c diff.noprefix=false diff --no-ext-diff --no-textconv -- <scope-paths>
   GIT_CONFIG_NOSYSTEM=1 GIT_TERMINAL_PROMPT=0 GIT_OPTIONAL_LOCKS=0 git -c core.pager=cat -c diff.external= -c diff.noprefix=false diff --cached --no-ext-diff --no-textconv -- <scope-paths>
   ```

   Run each command as a separate invocation using the fixed Git environment (disabling external diff, textconv, pagers, aliases, filters, prompts, credentials, fsmonitor, and network access) and require its exit status to be successful before continuing; a later successful command must not mask an earlier failure.

   Scope dirt inspection never substitutes for the shared type/mode/content or approved-preimage guards.

10. Require exactly one approved preimage-to-postimage transition per output. Immediately before an output's first write, reproduce its exact preimage through the shared non-following path-state contract. On resume: pending plus exact preimage may write; completed plus exact postimage continues; pending plus exact postimage is checkpointed as already applied only after exact verification; every other combination invalidates approval and aborts. A proposed diff never substitutes for tuple comparison.

| Scope | `<scope-paths>` |
|---|---|
| `all` / omitted | `AGENTS.md`, `.agent/`, and every material input/output path in the plan |
| `<group>` | `.agent/workflows/load-skills-<group>.md`, `.agent/skills/<group>-*/**` |
| `<skill-dir>` | `.agent/skills/<skill-dir>/**` |
| `skills` | `.agent/skills/**`, all `load-skills-*.md`, task workflows |
| `agents` | `AGENTS.md`, profile, solution/csproj files, task/loader workflows, skill index |
| `projects` | solution/csproj files and project-referencing skills |
| `infra` | build config, CI/CD, Docker files |
| `files: <paths>` | exact listed paths plus resolved stage output paths in the plan |
| `stage-<N>` | files touched by Stage N |

If dirty, abort with: `"Plan is stale — run /update again to regenerate the plan"`.

### Correction Mode

`/update-execute` is the sole source-mutation owner for a failed run.

1. Enter only from plan status `failed` or `correcting`. Require the verification progress Run ID, requested/effective scope, Semantic Plan SHA-256, and failed Output Revision SHA-256 to match the current failed run.
2. Read the stable finding IDs and targets from `Corrections Required`. Reject a target outside the current scope or Stage 1-5 `### Outputs`; scope or semantic-plan widening starts a fresh run.
3. Require the correction-labeled apply preview, exact diff, and fresh explicit approval produced by `/update`. Never reuse the original apply approval.
4. Initialize or resume `.agent/temp/update-correction-progress.md`:

   ```markdown
   # Update Correction Progress
   > Run ID: <run-id> | Status: correcting | Correction Preview SHA-256: <sha256>
   > Requested Scope: <scope> | Scope: <effective scope> | Semantic Plan SHA-256: <sha256>
   ## Corrections
   - [ ] <finding-id> | <exact target> | <action>
   ```

5. Set plan status to `correcting` before the first correction. Apply only pending approved corrections, checkpoint each item, and abort on any binding or target change.
6. When every correction is complete, set correction progress and plan status to `done`. The caller must review changed outputs before verification; correction never transitions directly to `reviewed` or `verifying`.

### Resumption And VCS

Resume from the first incomplete stage. Use Git read-only: no commit, push, stash, checkout, or ref mutation.

---

## 1. Stage 1: Sync Group Workflows

### Group Loaders

1. Preserve YAML frontmatter, descriptions, `// turbo` annotations, and skill order.
2. Regenerate file-read entries; append new skills at the end.
3. Update token estimate: total skill bytes / 4, rounded to 0.1K.
4. Create/update `.agent/workflows/load-skills-<custom>.md` for each custom prefix.
5. Use `load-skills-custom.md` only for uncategorized skills.
6. If a group is empty, remove its loader references from task workflows and `load-skills-all.md`; report the loader for manual deletion unless deletion was approved.

### Task Workflows

Discover task workflows in `.agent/workflows/*.md` except `load-skills-*.md`, including `documentation.md`, `commit-polish.md`, and `update*.md`.

Use the plan's workflow classification and `Custom Workflows` header as the execution contract. If current discovery finds a custom route missing from the plan, or a planned custom workflow no longer exists, abort with: `"Plan is stale — run /update again to regenerate the plan"`.

Sync only skill-loading or shared-workflow reference sections. Preserve all other instructions.

### Custom Group And Irrelevance Removal

- Create/update custom loaders per the planner's Custom loader rule.
- If irrelevance removal was approved, remove references from loaders/task workflows, flag Stage 3/5, and report directories for manual deletion. Never auto-delete directories.

---

## 2. Stage 2: Sync `load-skills-all.md`

Regenerate from all group loaders:

1. Preserve YAML and `// turbo-all`.
2. Emit group sections in Standard Load Order: Basic -> Overview -> DRN Framework -> Testing -> Frontend -> Custom.
3. In Custom, list per-prefix custom loaders alphabetically, then `load-skills-custom.md` if present.
4. Update total token estimate. Include every existing custom loader.

---

## 3. Stage 3: Sync `AGENTS.md` And Profile

Keep `AGENTS.md` portable. Put project-specific facts in `.agent/repository-profile.md`.

### Profile Overview And Commands

- Populate Type, Architecture, Frontend, and Testing from discovered projects.
- Update project names in `dotnet build`, unit test, and integration test commands.
- Preserve the repo rule: do not run build/test commands unless the user explicitly allows it.

### Skill Discovery And Custom Routes

1. Read the plan header and discovery summary first.
2. Treat `Custom Workflows: <route> -> <workflow>` as authoritative for Stage 3 and Stage 5.
3. Do not independently discover or reclassify a different custom route set during execution.
4. Use the plan classification for non-custom skill-loading, task, sub-workflow, and meta routes.
5. Update the `AGENTS.md` Workflows table from discovered task workflows; add new routes, remove stale routes, and preserve portable descriptions when the route already exists.
6. Keep custom route details in the profile: route, workflow file, trigger/intent, and required custom loaders.
7. Create/refresh custom skill load-set entries in the profile.
8. Preserve missing copied profile skills through the profile's `Missing Profile Extensions` table contract with exact status `⚠️ Missing profile reference`. Do not add missing skills to loaders, delete them from the profile, or treat them as Stage 6 drift.

### Project Prefix Rename

If a project prefix changed:

1. Scan `.agent/repository-profile.md` and repository-owned overlay skills; exclude generic/framework-scoped skills.
2. Present prefix mapping families for approval.
3. Apply boundary-aware regex:

   ```regex
   (?<=[ \t`'"\n\/]|^)<Prefix>\.
   ```

4. Verify every replaced path resolves.

---

## 4. Stage 4: Sync Non-Project References

Flag only. Verify references to build config, CI/CD actions, containers, and solution files in commands. Report stale items with the Evidence Contract. Do not modify files.

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
- Every actual Stage 1-5 source/configuration mutation matches an exact declared `### Outputs` path; otherwise the plan is stale.

`update-verify.md` owns content verification.
