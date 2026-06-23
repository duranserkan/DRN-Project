---
description: Execution phase of /update — read update-plan.md, execute sync stages
---

> **Sub-workflow of `/update`**. Not invoked directly. Reads `Scope` and follows Stage Resumption Protocol.
> See also: [Status Lifecycle](./_shared/status-lifecycle.md) · [Operating Model](./_shared/workflow-operating-model.md)
> **Estimated context: ~1.8K tokens**

## 0. Pre-Execution Validation

If invoked directly, run the shared Startup Gate; otherwise inherit `/update` context.

### Staleness Guard

1. Warn if the plan is older than 24 hours.
2. Warn if `Baseline HEAD` differs from current `HEAD`; it is audit metadata only.
3. Abort if material in-scope inputs exist and `Baseline Inputs Hash` is missing, malformed, `N/A`, or no longer matches current normalized in-scope inputs.
4. Allow `Baseline Inputs Hash: N/A` only when the plan header contains exactly `Baseline Inputs Hash Justification: no-material-input-files` and exact scope paths still contain no material inputs.
5. Abort if in-scope files have uncommitted changes not represented in the plan:

   ```bash
   git diff -- <scope-paths>
   git diff --cached -- <scope-paths>
   ```

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

`update-verify.md` owns content verification.
