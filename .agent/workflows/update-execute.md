---
description: Execution phase of /update — read update-plan.md, execute sync stages
---

> **Sub-workflow of `/update`** — not invoked directly. Reads `Scope` from plan header; skipped stages follow Stage Resumption Protocol (`update.md` §Plan File Contract).
>
> **Estimated context: ~2.8K tokens** (this workflow)

---

## 0. Pre-Execution Validation

### Staleness Guard

1. **Warn** if `Generated` timestamp > 24 hours old
2. **Abort** if in-scope files changed (unstaged **or** staged) since plan generation:

   ```bash
   # Both checks required — either indicates potential staleness
   git diff -- <scope-paths>          # unstaged changes
   git diff --cached -- <scope-paths>  # staged changes
   ```
   | Scope | `<scope-paths>` |
   |-------|-------|
   | `all` / *(omitted)* | `.agent/` |
   | `<group>` | `.agent/workflows/<group>.md` `.agent/skills/<group>-*/**` |
   | `<skill-dir>` | `.agent/skills/<skill-dir>/**` |
   | `skills` | `.agent/skills/**` `.agent/workflows/load-skills-{basic,overview,drn,frontend,test}.md` `.agent/workflows/review.md` |
   | `agents` | `AGENTS.md` + solution/csproj files + `.agent/workflows/load-skills-{basic,overview,drn,frontend,test}.md` + `.agent/skills/overview-skill-index/**` |
   | `projects` | Solution/csproj files + project-referencing skill paths |
   | `infra` | Non-project asset paths (`Directory.Build.props`, CI/CD, Docker) |
   | `stage-<N>` | Files touched by that specific stage |
3. On abort: `"Plan is stale — run /update again to regenerate the plan"`

### Stage Resumption

Follow Stage Resumption Protocol in `update.md` §Plan File Contract via `.agent/update-plan.md`.

> **VCS assumption**: Read-only git commands only — no commit, stash, checkout, or push.

---

## 1. Sync Group Workflows (Stage 1)

### 1.1 Group Loaders (`load-skills-basic.md`, `load-skills-overview.md`, `load-skills-drn.md`, `load-skills-frontend.md`, `load-skills-test.md`)

Per group loader:

1. **Preserve**: YAML frontmatter, `description`, `// turbo` annotations, existing skill order (dependency-first)
2. **Regenerate**: Skill list — one `view_file` per skill; append new at end, never reorder
3. **Update**: Token estimate — Σ(skill sizes, primary + cross-referenced) ÷ 4, rounded to 0.1K

#### Skill list template

```markdown
Read the skills:
   - `view_file .agent/skills/<skill-name>/SKILL.md`
   - `view_file .agent/skills/<skill-name>/SKILL.md`
   ...
```

### 1.2 Task Workflows (`test.md`, `review.md`, `develop.md`)

Sync **only** `view_file` entries within skill-loading sections:

1. **Identify** skill-loading section (e.g., `test.md` §2, `review.md` §2)
2. **Update** `view_file` lines — add missing, remove nonexistent
3. **Preserve** all surrounding prose, numbering, headings, procedural instructions
4. **Do not** apply the group loader template

### 1.3 Custom Group (if applicable)

Create `custom.md` if custom skills exist without it:

```markdown
---
description: Load custom project-specific skills
---

> **Estimated context: ~<N>K tokens** (<count> skills)

Read the skills:
   - `view_file .agent/skills/<custom-skill>/SKILL.md`
```

If `custom.md` exists, update its skill list per the same pattern.

---

## 2. Sync `load-skills-all.md` (Stage 2)

Regenerate `load-skills-all.md` from all group workflows:

1. **Preserve**: YAML frontmatter, `// turbo-all` annotation
2. **Regenerate**: All group sections with skill lists
3. **Update**: Total token estimate (Σ group estimates)
4. **Include**: Custom group section if `custom.md` exists

**Section order**: Follow the Standard Load Order in `update.md` §Standard Load Order (Basic → Overview → DRN Framework → Testing → Frontend → Custom).


---

## 3. Sync `AGENTS.md` (Stage 3)

Update **derived/structural sections only** — behavioral prose and conventions are untouched.

### 3.1 Project Overview Table

Populate from discovered projects:

| Aspect | Source |
|--------|--------|
| **Type** | Detected from solution/csproj |
| **Architecture** | Detected layer structure |
| **Frontend** | Detected from hosted project |
| **Testing** | DTT — integration-first with Testcontainers |

### 3.2 Key Commands

Update project names in build commands:

```bash
dotnet build <solution-file>              # Build solution
dotnet test <solution-file>               # Run all tests
```

### 3.3 Skill Discovery

Verify and update paths — skill index, load-all workflow, individual workflows. **Auto-discover** all `.md` files in `.agent/workflows/` and classify:

```markdown
- **Skill-loading workflows**: `.agent/workflows/load-skills-{basic,overview,drn,frontend,test}.md`
- **Task workflows**: `.agent/workflows/{clarify,develop,review,test,update}.md`
- **Sub-workflows**: `.agent/workflows/{update-plan,update-execute,update-verify}.md`
- **Meta workflow**: `.agent/workflows/load-skills-all.md` (loads all skill groups)
```

> Exclude `custom.md` if nonexistent. Include any newly discovered workflow files.

### 3.4 Conventions

**Do not modify** — hand-authored, project-specific.

### 3.5 Project Name Substitution in Skill Bodies

When the project prefix has changed (e.g., `Sample` → `MyApp`):

1. **Detect**: Scan skill files for **all** project prefixes (from `<Project>` entries in solution file). `DRN.Framework.*` prefixes are **never** substituted
2. **Scan**: `grep_search` for old prefix across `.agent/skills/*/SKILL.md`
3. **Present mapping** — show **all** prefix families separately:
   ```text
   Family 1: Sample.* → MyApp.*
     Sample.Hosted      → MyApp.Hosted
     Sample.Application  → MyApp.Application

   Family 2: DRN.Nexus.* → (no match — flag for removal or manual mapping)
     DRN.Nexus.Hosted    → ???
   ```
4. **Wait for user approval** (DiSCOS Autonomy Ladder level 4)
5. **Apply**: Boundary-aware find-and-replace — approved mappings only:
   ```regex
   Match: (?<=[ \t`'"\n\/]|^)<Prefix>\.
   ```
   Prefix must follow whitespace, BOL, backtick, quote, or `/` — prevents partial-match corruption (e.g., `SampleData`, `ExampleSample`).
6. **Verify**: Each substitution produces a valid path in the target repo

> Project-specific paths (e.g., `Sample.Hosted/vite.config.js`) are also updated.

> **⚠️ Manual attention**: Overview/architecture skills contain structural docs (ASCII trees, mermaid diagrams) reflecting the **original** layout — **flag for manual regeneration**.

> [!IMPORTANT]
> Business repos ported from DRN-Project typically need **two** substitution sets: sample/reference app + DRN.Nexus (if present). Always present all detected families.

---

## 4. Sync Non-Project References (Stage 4)

Validate and update references to non-project assets across skills and AGENTS.md.

### 4.1 Build Infrastructure

For skills referencing `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `nuget.config`:

1. Verify file exists → if missing: `⚠️ Stale non-project reference`
2. If exists but content changed significantly (e.g., target framework) → flag for user review

### 4.2 CI/CD

For `overview-github-actions` and skills referencing `.github/workflows/`:

1. Verify referenced workflow files + composite action paths exist
2. Flag missing or renamed workflows

### 4.3 Containers

For skills referencing Dockerfiles or docker-compose: verify files exist, flag stale references.

### 4.4 AGENTS.md Infrastructure

Verify referenced commands still work (e.g., solution file name in `dotnet build`). Update paths if renamed.

> [!NOTE]
> Non-project asset sync is read-and-flag only — no modifications without user approval, consistent with "flags, never auto-removes" principle.

---

## 5. Sync Skill Index (Stage 5)

Update `overview-skill-index/SKILL.md`:

### 5.1 YAML Frontmatter

Set `last-updated` to today's date.

### 5.2 "I want to…" Task Table

**Add** rows for new skills (derive task mappings from `description`) · **Remove** rows for deleted skills · **Preserve** hand-tuned rows

### 5.3 By Layer Tables

**Add** new skills under appropriate layer (infer from name/description) · **Remove** deleted · **Preserve** existing

### 5.4 Skill Dependency Graph

**Add** new nodes (standalone unless dependency is obvious) · **Remove** deleted nodes · **Preserve** existing arrows (hand-maintained)

### 5.5 Keyword Index

**Add** keywords from new skills' `description` frontmatter · **Remove** keywords only associated with deleted skills · **Preserve** existing mappings

---

## 6. Sync Project Docs (Stage 6)

> **Flag-only stage** — never auto-modifies project documentation content.

Uses the plan's `### Documentation Drift` data and `Prefix mapping` (if any). If no drift and no rename: mark stage `skipped`.

### 6.1 Read Plan Drift Data

From plan Discovery Summary, extract:

- **`### Documentation Drift`** table — modules with STALE > 0, MISSING > 0, or RENAMED > 0
- **`### Drift Report` → `Prefix mapping`** — old → new prefix (if rename occurred)

Process only modules with detected drift or prefix mapping hits. `[STUB]` modules → report as deferred.

### 6.2 Scan for Stale References (project rename only)

If `Prefix mapping` exists, scan per-module `README.md` and `RELEASE-NOTES.md` for old prefix:

```text
grep_search "<OldPrefix>" <Module>/README.md
grep_search "<OldPrefix>" <Module>/RELEASE-NOTES.md
# Repeat for each old prefix family
```

Also flag any file paths or project names that no longer resolve (removed projects).

### 6.3 Report Findings

Present a unified flag-only report — do **not** modify files:

```markdown
## Stage 6: Project Docs Flags

### Content Drift *(from §2.2 pre-scan)*

| Module | STALE | MISSING | RENAMED | Details |
|--------|-------|---------|---------|---------|
| DRN.Framework.X | 2 | 1 | 0 | `[STALE] OldType`, `[MISSING] NewFeature` |

### Stale Project References *(from prefix mapping — omit if no rename)*

| File | Stale Reference | Recommended Action |
|------|----------------|--------------------| 
| README.md:L42 | `OldPrefix.Hosted` | Replace with `NewPrefix.Hosted` |

### Stub Modules *(deferred)*
- DRN.Framework.Jobs — stub, no content to drift-scan
```

If no drift detected and no stale references: *"Stage 6: No documentation drift detected."*

### 6.4 Delegation Offer (if flags exist)

After presenting the flag report, offer delegation to `/documentation`:

```text
Stage 6 flagged content drift in: DRN.Framework.X, DRN.Framework.Y
Delegate content update to /documentation for each module? (Y/N)
```

- **Y** → invoke `/documentation <module>` for each flagged module in turn. `/documentation` §8 confirmation gate applies — content is never written without explicit user sign-off.
- **N** → flag report complete; author makes edits manually.

---

## 7. Plan Completion

1. Verify all plan stages `done` with no unchecked action items
2. Update plan status to `done`
3. Report completion — orchestrator routes to `review.md`

> Verification checks (structural integrity, cross-references, token estimates, source-code alignment) are handled by `update-verify.md` §1–§3.
