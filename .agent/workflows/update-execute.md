---
description: Execution phase of /update — read update-plan.md, execute sync stages
---

> **Sub-workflow of `/update`** — called by the orchestrator, not invoked directly.
>
> Reads `Scope` from the plan header. Skipped stages follow the Stage Resumption Protocol in `update.md` §Plan File Contract.

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
   | `skills` | `.agent/skills/**` `.agent/workflows/{basic,overview,drn,frontend,test}.md` |
   | `agents` | `AGENTS.md` + solution/csproj files + `.agent/workflows/{basic,overview,drn,frontend,test}.md` + `.agent/skills/overview-skill-index/**` |
   | `projects` | Solution/csproj files + project-referencing skill paths |
   | `infra` | Non-project asset paths (`Directory.Build.props`, CI/CD, Docker) |
   | `stage-<N>` | Files touched by that specific stage |
3. On abort: `"Plan is stale — run /update again to regenerate the plan"`

### Stage Resumption

Follow Stage Resumption Protocol in `update.md` §Plan File Contract via `.agent/update-plan.md`.

> **VCS assumption**: The user owns Git state (commit, stash, checkout). The agent never manipulates VCS. Read-only git commands (`git log`, `git diff --cached`) are permitted for staleness detection; the agent never writes to VCS (no commit, stash, checkout, or push).

---

## 1. Sync Group Workflows (Stage 1)

### 1.1 Group Loaders (`basic.md`, `overview.md`, `drn.md`, `frontend.md`)

Per group loader:

1. **Preserve**: YAML frontmatter, `description`, `// turbo` annotations, existing skill order (dependency-first — intentional)
2. **Regenerate**: Skill list — one `view_file` per skill; append new skills at end, never reorder
3. **Update**: Token estimate in the `> **Estimated context:` line
   - Formula: Σ(skill file sizes in workflow, primary + cross-referenced) ÷ 4, rounded to nearest 0.1K
   - *Note*: This is a conservative approximation for LLM context safety; it does not account for protocol overhead or metadata.
   - Cross-referenced skills contribute independently to each workflow's estimate

#### Skill list template

```markdown
Read the skills:
   - `view_file .agent/skills/<skill-name>/SKILL.md`
   - `view_file .agent/skills/<skill-name>/SKILL.md`
   ...
```

### 1.2 Task Workflows (`test.md`, `review.md`)

Task workflows have procedural structure — sync **only** `view_file` entries within their skill-loading section:

1. **Identify** the skill-loading section (e.g., `test.md` §2, `review.md` §2)
2. **Update** `view_file` lines only — add missing, remove nonexistent
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

## 2. Sync `all.md` (Stage 2)

Regenerate `all.md` from all group workflows:

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
| **CI/CD** | Detected from `.github/workflows/` |

### 3.2 Key Commands

Update project names in build commands:

```bash
dotnet build <solution-file>              # Build solution
dotnet test <solution-file>               # Run all tests
dotnet run --project <hosted-project>     # Run app
```

### 3.3 Skill Discovery

Verify and update paths — skill index, load-all workflow, individual workflows. **Auto-discover** all `.md` files in `.agent/workflows/` and classify:

```markdown
- **Skill-loading workflows**: `.agent/workflows/{basic,overview,drn,frontend}.md`
- **Task workflows**: `.agent/workflows/{review,test,update}.md`
- **Sub-workflows**: `.agent/workflows/{update-plan,update-execute,update-verify}.md`
- **Meta workflow**: `.agent/workflows/all.md` (loads all skill groups)
```

> Exclude `custom.md` if nonexistent. Include any newly discovered workflow files.

### 3.4 Conventions

**Do not modify** — hand-authored, project-specific.

### 3.5 Project Name Substitution in Skill Bodies

When the project prefix has changed (e.g., `Sample` → `MyApp`):

1. **Detect**: Scan skill files for **all** project prefixes (from `<Project>` entries in solution file). Framework prefixes (`DRN.Framework.*`) are **never** substituted
2. **Scan**: `grep_search` for old prefix across `.agent/skills/*/SKILL.md`
3. **Present mapping** — show **all** prefix families separately:
   ```
   Family 1: Sample.* → MyApp.*
     Sample.Hosted      → MyApp.Hosted
     Sample.Application  → MyApp.Application
     ...

   Family 2: DRN.Nexus.* → (no match — flag for removal or manual mapping)
     DRN.Nexus.Hosted    → ???
   ```
4. **Wait for user approval** — modifies hand-authored content (DiSCOS Autonomy Ladder level 4)
5. **Apply**: Find-and-replace approved mappings only; use a boundary-aware match:
   ```
   Match: (?<=[ \t`'"\n\/]|^)<Prefix>\.
   ```
   The prefix must be immediately preceded by whitespace, BOL, a backtick, a quote (`'`, `"`), or `/` — never by another identifier character. This prevents partial-match corruption of identifiers like `SampleData`, `ExampleSample`, or `SampleTest`.
   - Example: `SampleTest.Sample.Hosted` — `SampleTest.` does **not** match (preceded by BOL, but `SampleTest` itself contains `Sample` only as a substring, not at a boundary); `` `Sample.Hosted` `` **does** match (backtick precedes `Sample.`).
6. **Verify**: Each substitution produces a valid path in the target repo

> Skills with project-specific paths (e.g., `Sample.Hosted/vite.config.js`) will also have paths updated.

> **⚠️ Manual attention**: Overview/architecture skills (`overview-repository-structure`, `overview-ddd-architecture`) contain structural documentation (ASCII trees, mermaid diagrams, layer tables) reflecting the **original** repo layout. Name substitution cannot fix structural differences — **flag for manual regeneration** and present the list.

> [!IMPORTANT]
> Business repos ported from DRN-Project typically need **two** independent substitution sets: sample/reference app + DRN.Nexus (if present). Always present all detected families.

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

Applies only when a project rename (`Prefix mapping` in Discovery Summary) or structural change (projects added/removed) occurred. Otherwise: mark stage `skipped`.

### 6.1 Identify Candidate Files

```
find_by_name README.md      # repo root and project subdirectories
find_by_name RELEASE-NOTES.md
find_by_name ROADMAP.md
find_by_name CHANGELOG.md
list_dir docs/              # if exists
```

### 6.2 Scan for Stale References

For each candidate file, `grep_search` for the **old** project prefix(es) from the Discovery Summary `Prefix mapping`:

```
grep_search "<OldPrefix>" README.md
grep_search "<OldPrefix>" RELEASE-NOTES.md
grep_search "<OldPrefix>" ROADMAP.md
grep_search "<OldPrefix>" CHANGELOG.md
# Repeat for each old prefix family
```

Also flag any file paths or project names that no longer resolve (removed projects).

### 6.3 Report Findings

Present a flag-only report — do **not** modify files:

```markdown
## Stage 6: Project Docs Flags

| File | Stale Reference | Recommended Action |
|------|----------------|--------------------|
| README.md:L42 | `OldPrefix.Hosted` | Replace with `NewPrefix.Hosted` |
| RELEASE-NOTES.md:L10 | `OldPrefix.Utils` | Replace with `NewPrefix.Utils` |
| ROADMAP.md:L18 | `OldPrefix.Api` | Replace with `NewPrefix.Api` |
```

If no stale references found: *"Stage 6: No stale project doc references detected."*

---

## 7. Plan Completion

1. Verify all plan stages have status `done` with no unchecked action items
2. Update plan overall status to `done`
3. Report completion — orchestrator routes to `review.md` next

> Structural integrity, cross-reference, token estimate, and source-code alignment checks are handled by `update-verify.md` — specifically §1, §2, and §3.
