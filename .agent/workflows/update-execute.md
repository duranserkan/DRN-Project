---
description: Execution phase of /update — read update-plan.md, execute sync stages
---

> **Sub-workflow of `/update`** — not invoked directly. Reads `Scope` and follows Stage Resumption Protocol.
> See also: [Status Lifecycle](./_shared/status-lifecycle.md) · [Operating Model](./_shared/workflow-operating-model.md)
> **Estimated context: ~1.5K tokens**

---

## 0. Pre-Execution Validation

### Staleness Guard
If invoked directly, apply the shared Startup Gate before work; otherwise inherit `/update` startup context.

1. **Warn** if plan is > 24 hours old.
2. **Warn** if `Baseline HEAD` differs from current `HEAD`; it is audit metadata, not a hard gate.
3. **Abort** if material in-scope inputs exist and `Baseline Inputs Hash` is missing, malformed, `N/A`, or no longer matches the current normalized in-scope inputs.
4. **Allow** `Baseline Inputs Hash: N/A` only after confirming the plan header contains exactly `Baseline Inputs Hash Justification: no-material-input-files` and re-checking exact scope paths still finds no material inputs; otherwise abort as stale.
5. **Abort** if in-scope files have uncommitted changes not represented in the plan:

   ```bash
   git diff -- <scope-paths>          # unstaged
   git diff --cached -- <scope-paths>  # staged
   ```

| Scope | `<scope-paths>` |
|-------|-------|
| `all` / *(omitted)* | `AGENTS.md`, `.agent/`, and every material input/output path recorded in the plan |
| `<group>` | `.agent/workflows/load-skills-<group>.md` and `.agent/skills/<group>-*/**` |
| `<skill-dir>` | `.agent/skills/<skill-dir>/**` |
| `skills` | `.agent/skills/**` and all `load-skills-*.md` loaders and task workflows |
| `agents` | `AGENTS.md` + `.agent/repository-profile.md` + solution/csproj files + task/loader workflows + skill index |
| `projects` | Solution/csproj files + project-referencing skills |
| `infra` | Build config, CI/CD, Docker files |
| `files: <paths>` | Exact listed paths plus resolved stage output paths recorded in the plan |
| `stage-<N>` | Files touched by stage N |

If dirty: abort with `"Plan is stale — run /update again to regenerate the plan"`.

### Resumption & VCS Rules
Resume from the first incomplete stage. Git commands are **read-only** (no commit/push/stash/checkout).

---

## 1. Sync Group Workflows (Stage 1)

### 1.1 Group Loaders (`load-skills-*.md`)
1. **Preserve**: YAML frontmatter, descriptions, `// turbo` annotations, and skill ordering.
2. **Regenerate**: file-read entries; append new at the end.
3. **Update**: Token estimate = Sum of skill sizes ÷ 4, rounded to 0.1K.
4. **Custom prefixes**: For each discovered `<custom>-*` prefix group, create/update `.agent/workflows/load-skills-<custom>.md`. Use `.agent/workflows/load-skills-custom.md` only for uncategorized skill directories that do not map cleanly to a prefix group.
5. **Removed groups**: If a group has no remaining skills, remove its loader references from task workflows and `load-skills-all.md`; report the empty loader for manual deletion unless the user explicitly approved deletion.

### 1.2 Task Workflows
Discover task workflows in `.agent/workflows/*.md` except `load-skills-*.md`, including meta/sub-workflows such as `documentation.md`, `commit-polish.md`, and `update*.md`.
Sync only skill-loading or shared-workflow reference sections. Preserve surrounding instructions.
When a task workflow is repository-specific, ensure its route is recorded in the plan's `Custom Workflows` header for Stage 3 and Stage 5.

### 1.3 Custom Group & Irrelevance Removal
- Create/update per-prefix custom loaders and `load-skills-custom.md` according to the planner's Custom loader rule.
- **Irrelevance Removal** (if user-approved): Remove from loaders/task workflows, flag for Stage 3/5, and report directories for manual deletion (never auto-delete).

---

## 2. Sync `load-skills-all.md` (Stage 2)
Regenerate from all group loader workflows:
1. **Preserve**: YAML, `// turbo-all` annotation.
2. **Regenerate**: All group sections in Standard Load Order (Basic → Overview → DRN Framework → Testing → Frontend → Custom).
3. **Custom ordering**: Within `Custom`, list per-prefix custom loaders alphabetically, then `load-skills-custom.md` if it exists.
4. **Update**: Sum total token estimate. Include every custom loader that exists.

---

## 3. Sync `AGENTS.md` And Repository Profile (Stage 3)
Keep `AGENTS.md` portable (agnostic prose). Update project-specific facts in `.agent/repository-profile.md`.

### 3.1 Profile Project Overview Table
Populate from discovered projects:
- *Type*: Detected from solution/csproj.
- *Architecture*: Detected layer structure.
- *Frontend*: Detected from hosted project.
- *Testing*: Detected test projects/config.

### 3.2 Profile Key Commands
Update project names in `dotnet build`, `dotnet run --project <unit>`, and `dotnet run --project <integration>`. Preserve the repo rule: do not run unless user explicitly allows it.

### 3.3 AGENTS.md Skill Discovery
Discover workflows and classify:
- *Skill-loading*: `load-skills-*.md` (except custom if absent).
- *Task / Sub-workflows / Meta*: task `.md` files.

Then sync derived routing:
1. Update the `AGENTS.md` Workflows table from discovered task workflows. Add new routes, remove stale routes, and preserve portable descriptions when the route already exists.
2. Keep repository-specific facts out of `AGENTS.md`. For custom routes, keep the AGENTS row generic and put exact project triggers, workflow ownership, and required skill loaders in `.agent/repository-profile.md`.
3. In `.agent/repository-profile.md`, create or refresh a `Custom Workflow Routes` section when custom task workflows exist. Record route, workflow file, trigger/intent, and required custom skill loaders.
4. In `.agent/repository-profile.md`, create or refresh custom skill load-set entries for discovered custom prefix groups, including missing-skill handling when a copied profile references skills not present in the new repository.

### 3.4 Project Name Substitution (Prefix Rename)
If a project prefix changed:
1. Scan `.agent/repository-profile.md` and repository-owned overlay skills. (Generic/framework-scoped skills are excluded).
2. Present prefix mapping families for approval.
3. Apply boundary-aware regex:

   ```regex
   (?<=[ \t`'"\n\/]|^)<Prefix>\.
   ```

4. Verify all replaced paths resolve.

---

## 4. Sync Non-Project References (Stage 4)
*Flag-only stage*: Verify references to non-project assets (Build configs, CI/CD actions, containers, solution file in build commands). Report stale items using the shared Evidence Contract; do not modify.

---

## 5. Sync Skill Index (Stage 5)
Update `overview-skill-index/SKILL.md`:
- Set `last-updated` to today.
- Update Task Table, By Layer Tables, Dependency Graph (Mermaid), and Keyword Index. Add new, remove deleted, preserve custom tweaks.
- Add or remove entries for custom task workflow routes, custom skill prefixes, and `new repository` / `port .agent` / `self-sync` routing.
- Ensure every skill directory referenced by the index exists, and every discovered custom skill group has an index route through the profile/custom overlay language.

---

## 6. Sync Project Docs (Stage 6)
*Flag-only stage*: Flag drift in READMEs/skills based on plan data (stale, missing, renamed).
- **Report Findings Template**:

  ```markdown
  ## Stage 6: Project Docs Flags
  ### Content Drift / Skill Content Drift / Stale Project References
  - Family.Module: Evidence: <file:line> | Impact: <risk> | Invariant: <rule> | Recommendation: <delegate/fix> | Confidence: high/medium/low | Verification: run/not run/blocked/N/A
  ```

- **Delegation Offer**: Ask user: *"Delegate updates to /documentation for each module? (Y/N)"*. Y loads `documentation.md` with the affected module scope and its preview gate.

---

## 7. Plan Completion
Verify every stage is terminal, then set plan status to `done` and report. (Content verification is handled next by `update-verify.md`).
- In-scope stages must be `done`.
- Out-of-scope stages may remain `skipped`.
- Any `pending`, `executing`, `blocked`, `fail`, or unresolved `Requires Approval` item blocks completion.
