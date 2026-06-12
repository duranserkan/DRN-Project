---
description: Execution phase of /update — read update-plan.md, execute sync stages
---

> **Sub-workflow of `/update`** — not invoked directly. Reads `Scope` and follows Stage Resumption Protocol.
> **Estimated context: ~1.5K tokens**

---

## 0. Pre-Execution Validation

### Staleness Guard
1. **Warn** if plan is > 24 hours old.
2. **Abort** if in-scope files changed since plan generation:
   ```bash
   git diff -- <scope-paths>          # unstaged
   git diff --cached -- <scope-paths>  # staged
   ```

| Scope | `<scope-paths>` |
|-------|-------|
| `all` / *(omitted)* | `.agent/` |
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
2. **Regenerate**: `view_file` entries; append new at the end.
3. **Update**: Token estimate = Sum of skill sizes ÷ 4, rounded to 0.1K.

### 1.2 Task Workflows (`test.md`, `review.md`, `develop.md`)
Sync only the `view_file` list in the skill-loading sections. Preserve surrounding instructions.

### 1.3 Custom Group & Irrelevance Removal
- Create/update `load-skills-custom.md` if custom skills exist.
- **Irrelevance Removal** (if user-approved): Remove from loaders/task workflows, flag for Stage 3/5, and report directories for manual deletion (never auto-delete).

---

## 2. Sync `load-skills-all.md` (Stage 2)
Regenerate from all group loader workflows:
1. **Preserve**: YAML, `// turbo-all` annotation.
2. **Regenerate**: All group sections in Standard Load Order (Basic → Overview → DRN Framework → Testing → Frontend → Custom).
3. **Update**: Sum total token estimate. Include custom group if it exists.

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
*Flag-only stage*: Verify references to non-project assets (Build configs, CI/CD actions, containers, solution file in build commands). Report stale items; do not modify.

---

## 5. Sync Skill Index (Stage 5)
Update `overview-skill-index/SKILL.md`:
- Set `last-updated` to today.
- Update Task Table, By Layer Tables, Dependency Graph (Mermaid), and Keyword Index. Add new, remove deleted, preserve custom tweaks.

---

## 6. Sync Project Docs (Stage 6)
*Flag-only stage*: Flag drift in READMEs/skills based on plan data (stale, missing, renamed).
- **Report Findings Template**:
  ```markdown
  ## Stage 6: Project Docs Flags
  ### Content Drift / Skill Content Drift / Stale Project References
  - Family.Module: [details of drift]
  ```
- **Delegation Offer**: Ask user: *"Delegate updates to /documentation for each module? (Y/N)"*. Y runs `/documentation <module>`.

---

## 7. Plan Completion
Verify all stages are `done`, set plan status to `done`, and report. (Content verification is handled next by `update-verify.md`).
