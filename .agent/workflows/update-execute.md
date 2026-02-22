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
2. **Abort** if in-scope files changed since plan generation:

   ```bash
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

### 1.1 Group Loaders (`load-skills-{basic,overview,drn,frontend,test}.md`)

Per group loader:

1. **Preserve**: YAML frontmatter, `description`, `// turbo` annotations, existing skill order (dependency-first)
2. **Regenerate**: Skill list — one `view_file` per skill; append new at end, never reorder
3. **Update**: Token estimate — Σ(skill sizes, primary + cross-referenced) ÷ 4, rounded to 0.1K

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
3. **Preserve** all surrounding prose, numbering, headings, procedural instructions — do **not** apply group loader template

### 1.3 Custom Group (if applicable)

Create `custom.md` if custom skills exist without it. If it exists, update its skill list.

```markdown
---
description: Load custom project-specific skills
---

> **Estimated context: ~<N>K tokens** (<count> skills)

Read the skills:
   - `view_file .agent/skills/<custom-skill>/SKILL.md`
```

### 1.4 Irrelevance Removal (if applicable)

When plan §3.4 flags `⚠️ Possibly irrelevant` skills **and user approved** removal:

| Action | Scope |
|--------|-------|
| Remove `view_file` entries | Primary group loader |
| Remove from `load-skills-all.md` | Deferred to Stage 2 regeneration |
| Remove from task workflows | `review.md`, `test.md`, `develop.md` if referenced |
| Flag for removal | `AGENTS.md` (Stage 3), `overview-skill-index` (Stage 5) |
| Report skill directories | For manual deletion — **never** auto-deleted |

> [!IMPORTANT]
> Removal requires explicit user approval during planning (§3.4 `Requires Approval`). Never auto-remove.

---

## 2. Sync `load-skills-all.md` (Stage 2)

Regenerate from all group workflows:

1. **Preserve**: YAML frontmatter, `// turbo-all` annotation
2. **Regenerate**: All group sections with skill lists
3. **Update**: Total token estimate (Σ group estimates)
4. **Include**: Custom group section if `custom.md` exists

**Section order**: Standard Load Order per `update.md` §Standard Load Order (Basic → Overview → DRN Framework → Testing → Frontend → Custom).

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

Verify and update paths — skill index, load-all workflow, individual workflows. Auto-discover all `.md` files in `.agent/workflows/` and classify:

| Category | Pattern |
|----------|---------|
| Skill-loading | `load-skills-{basic,overview,drn,frontend,test}.md` |
| Task | `{clarify,develop,review,test,update}.md` |
| Sub-workflows | `{update-plan,update-execute,update-verify}.md` |
| Meta | `load-skills-all.md` |

Exclude `custom.md` if nonexistent. Include newly discovered workflow files.

### 3.4 Conventions

**Do not modify** — hand-authored, project-specific.

### 3.5 Project Name Substitution in Skill Bodies

When the project prefix has changed (e.g., `Sample` → `MyApp`):

1. **Detect**: Scan skill files for all project prefixes (from `<Project>` entries in solution file). Prefixes not matching any current project are **never** substituted — flag as external references
2. **Scan**: `grep_search` for old prefix across `.agent/skills/*/SKILL.md`
3. **Present mapping** — show all prefix families separately:
   ```text
   Family 1: Sample.* → MyApp.*
     Sample.Hosted      → MyApp.Hosted
     Sample.Application  → MyApp.Application

   Family 2: OldLib.* → (no match — flag for removal or manual mapping)
      OldLib.Core    → ???
   ```
4. **Wait for user approval** (DiSCOS Autonomy Ladder level 4)
5. **Apply**: Boundary-aware find-and-replace — approved mappings only:
   ```regex
   Match: (?<=[ \t`'"\n\/]|^)<Prefix>\.
   ```
   Prefix must follow whitespace, BOL, backtick, quote, or `/` — prevents partial-match corruption.
6. **Verify**: Each substitution produces a valid path in the target repo

> Project-specific paths (e.g., `Sample.Hosted/vite.config.js`) are also updated.

> **⚠️ Manual attention**: Overview/architecture skills contain structural docs (ASCII trees, mermaid diagrams) reflecting the **original** layout — flag for manual regeneration.

> [!IMPORTANT]
> Business repos ported from a reference project typically need multiple substitution sets — one per project-family prefix. Always present all detected families.

---

## 4. Sync Non-Project References (Stage 4)

Validate and update references to non-project assets across skills and AGENTS.md. **Flag-only** — no modifications without user approval.

| Category | Assets | Action |
|----------|--------|--------|
| **Build infrastructure** | `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `nuget.config` | Verify exists → if missing: `⚠️ Stale`; if content changed significantly → flag for review |
| **CI/CD** | `.github/workflows/`, composite actions | Verify referenced files exist → flag missing/renamed |
| **Containers** | Dockerfiles, docker-compose | Verify exists → flag stale references |
| **AGENTS.md infrastructure** | Solution file name in `dotnet build` etc. | Verify commands resolve → update paths if renamed |

---

## 5. Sync Skill Index (Stage 5)

Update `overview-skill-index/SKILL.md`:

### 5.1 YAML Frontmatter

Set `last-updated` to today's date.

### 5.2–5.5 Index Sections

All sections follow the same pattern: **Add** new · **Remove** deleted · **Preserve** hand-tuned content.

| Section | Content | Add Source |
|---------|---------|------------|
| **"I want to…" Task Table** | Task→skill mappings | Derive from `description` frontmatter |
| **By Layer Tables** | Skills under layers | Infer layer from name/description |
| **Dependency Graph** | Mermaid nodes+arrows | New skills standalone unless dependency obvious |
| **Keyword Index** | Keyword→skill mappings | Extract from `description` frontmatter |

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

If `Prefix mapping` exists, scan per-module `README.md` and `RELEASE-NOTES.md` for old prefix. Also flag file paths or project names that no longer resolve.

### 6.3 Report Findings

Present a unified flag-only report:

```markdown
## Stage 6: Project Docs Flags

### Content Drift *(from §2.2 pre-scan)*

| Module | STALE | MISSING | RENAMED | Details |
|--------|-------|---------|---------|---------| 
| <Family>.<Module> | 2 | 1 | 0 | `[STALE] OldType`, `[MISSING] NewFeature` |

### Skill Content Drift *(omit if none)*

| Module | Skill | Stale References | Details |
|--------|-------|-----------------|----------|
| <Family>.<Module> | `<prefix>-<suffix>/SKILL.md` | 2 | `OldMethod` not found |

> `/documentation` §3 step 5 handles skill content updates alongside README updates.

### Stale Project References *(omit if no rename)*

| File | Stale Reference | Recommended Action |
|------|----------------|--------------------| 
| README.md:L42 | `OldPrefix.Hosted` | Replace with `NewPrefix.Hosted` |

### Stub Modules *(deferred)*
- <Family>.<Module> — stub, no content to drift-scan
```

If no drift detected: *"Stage 6: No documentation drift detected."*

### 6.4 Delegation Offer (if flags exist)

```text
Stage 6 flagged drift in: <Family>.<ModuleA>, <Family>.<ModuleB>
  Content drift: <Family>.<ModuleA> (README), <Family>.<ModuleB> (README)
  Skill content drift: <Family>.<ModuleA> (<prefix>-<suffix>/SKILL.md)
Delegate updates to /documentation for each module? (Y/N)
```

- **Y** → Invoke `/documentation <module>` for each flagged module. All confirmation gates apply.
- **N** → Flag report complete; author makes edits manually.

> [!NOTE]
> `/documentation` re-runs its own drift scan (§3) which may be more granular. The `/update` flags serve as a trigger, not a replacement.

---

## 7. Plan Completion

1. Verify all plan stages `done` with no unchecked action items
2. Update plan status to `done`
3. Report completion — orchestrator routes to `review.md`

> Verification checks (structural integrity, cross-references, token estimates, source-code alignment) are handled by `update-verify.md` §1–§3.
