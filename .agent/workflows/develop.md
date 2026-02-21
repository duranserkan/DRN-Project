---
description: Implement requirements from a clarified document using DiSCOS, AGENTS.md and repository skills guidance
---

> **Pipeline**: `/clarify` → `/answer` → `/develop` (3/3) · [Status Lifecycle](./_shared/status-lifecycle.md)
>
> **Estimated context: ~2.0K tokens** (this workflow)

---

## 1. Resolve Input

- **Explicit path** (e.g., `/develop DEVELOP-x.md` or `/develop CLARIFY-x.md`) → Use that file.
- **No arguments** → Scan root for `DEVELOP-*.md` first, then `CLARIFY-*.md`.
  - One found → Use it. Multiple → Ask user. None → Direct to `/clarify` then `/answer`.
- **Inline requirements without doc** → Direct to `/clarify` then `/answer`.

---

## 2. Validate Status

Read YAML frontmatter `status`:

| Status | Action |
|---|---|
| `ready-to-develop` / `clarified` | ✅ Proceed to §3 |
| `draft` / `clarifying` / `draft-self-reviewed` | ❌ Not ready → direct to `/clarify` + `/answer` |
| `implemented` | ⚠️ Warn already implemented. Continue only on user confirmation. |
| Missing/other | ❌ Invalid → direct to `/clarify` + `/answer` |

### 2a. CLARIFY-*.md Lightweight Gate

When input is `CLARIFY-*.md` (skipping `/answer`), verify before proceeding:

- [ ] No `[ASSUMPTION - unverified]` tags in PBIs
- [ ] Every PBI has acceptance criteria
- [ ] Security implications addressed where relevant

Any failure → direct to `/answer`.

### 2b. Staleness Check

If input is `DEVELOP-*.md` with a `source` field → compare modification dates.
If source `CLARIFY-*.md` is newer → warn user that development document may be out of sync.
Recommend re-running `/answer` §7 or confirming content is current.

---

## 3. Load Context & Skills

### 3a. Read Project Guidance

- `view_file AGENTS.md` — behavioral framework, conventions
- `view_file .agent/skills/overview-skill-index/SKILL.md` — task→skill lookup
- `view_file .agent/skills/basic-agentic-development/SKILL.md` — Autonomy Ladder and Development Loop (§4, §5)

> If `/clarify` or `/answer` ran in the same session, reuse loaded context; re-read only if session is new or context was evicted.

### 3b. Select & Load Relevant Skills

Use skill index to load **only** what PBIs need (Context Economy):

| PBI Domain | Likely Skills |
|---|---|
| Domain/entity design | `drn-domain-design`, `drn-sharedkernel`, `drn-entityframework` |
| API/hosting | `drn-hosting`, `basic-security-checklist` |
| Frontend/UI | `frontend-*` skills |
| Testing | `test-unit`, `test-integration`, `test-integration-api`, `test-integration-db` |
| Infrastructure | `overview-repository-structure`, `overview-github-actions` |
| Documentation | `basic-documentation`, `basic-documentation-diagrams` |

### 3c. Read Guidance Sections

Read `Architecture Guidance` (from `DEVELOP-*.md`) or `Discovery & Guidance` (from `CLARIFY-*.md`) based on the document type.

### 3d. Scope Selection (Optional)

If user provided scope (e.g., `/develop CLARIFY-x.md EPIC-001` or `PBI-001 PBI-002`):
- Filter backlog to specified EPICs/PBIs.
- Warn if selected PBIs depend on unselected PBIs.

No scope specified → implement **entire backlog** in priority order.

---

## 4. Plan Implementation

For each PBI (in dependency and priority order):

1. **Identify affected files** — existing to modify, new to create
2. **Map to implementation tasks** — concrete code changes
3. **Identify risks** — breaking changes, security, schema changes
4. **Estimate complexity** — trivial / standard / significant / critical (per Autonomy Ladder)
5. **Assumption Check** — If `[ASSUMPTION - unverified]` found → return to `/answer` (preferred) or `/clarify` (scope change). If assumption cannot be resolved before development, **halt and escalate** — do not implement the PBI under an unresolved assumption.
6. **Conflict Resolution** — Competing constraints → **TRIZ** first, then **Priority Stack**

Present implementation plan:
- **Trivial/Standard** → Summarize briefly, proceed after presenting
- **Significant/Critical** → Detailed plan, wait for explicit approval

**Multi-PBI tracking** (3+ PBIs): maintain task checklist (planned → in-progress → done).

### 4a. Version Control Setup

- Check current branch (`git branch --show-current`). If already on task branch → use it.
- Otherwise → attempt `git checkout -b feature/[task-name-or-id] develop`
  - **Hotfix on master**: use `fix/[id] master` instead — see `basic-git-conventions` for merge rules.
  - **Permission failure**: if branch creation fails (permission denied, protected branch, read-only repo) → inform user, then **continue on current branch**. Do not block.
- Commit per PBI/logical chunk using conventional commits (`feat(Scope): description`)
- **Do not push.** Local commits serve as rollback checkpoints.

---

## 5. Execute

Follow Development Loop from `basic-agentic-development`:

### Per PBI (in planned order):

1. **Discovery** — Read existing code (`view_file_outline` first, then targeted reads)
2. **Implement** — Incrementally, smallest testable unit first
3. **Follow conventions** — Use patterns from loaded skills (verify with `grep_search`)
4. **Clean Code Gate** — enforce before proceeding to next PBI:
   - **Separation of concerns** — no business logic in controllers/handlers; no persistence logic leaking into domain
   - **Method extraction** — methods > 20 lines or doing > 1 thing → extract
   - **Cyclomatic complexity** — no method with CC > 5 without explicit justification
   - **Naming** — names reveal intent; no abbreviations, no generic identifiers (`data`, `result`, `obj`)
   - **No dead code** — no commented-out blocks, unused parameters, or unreachable branches

   > **Override**: Any rule may be overridden when necessary complexity genuinely requires it. Document the justification inline (code comment + commit message). Silently skipping is not an override.

5. **Validate**:
   ```bash
   dotnet build DRN.slnx
   ```
   - **Build fails (code error)** → Self-Correction Loop applies.
   - **Cannot execute** (toolchain missing, permission denied) → inform user, skip build validation, continue.
6. **Write tests** (DTT philosophy):
   - Pure logic → Unit tests
   - Persistence/queries → Integration tests (Testcontainers)
   - API endpoints → API integration tests
7. **Run tests**:
   ```bash
   dotnet test DRN.slnx
   ```
   - **Tests fail (code error)** → Self-Correction Loop applies.
   - **Cannot execute** (toolchain missing, permission denied) → inform user, skip test run, continue.

### Self-Correction Loop

- ❌ Build/test fails → Read error → Fix → Re-verify → Continue
- ⚠️ After **2 failed attempts** on same issue → Stop. Report: what tried, what failed, hypotheses, recommended next steps.

---

## 6. Verify

After all PBIs implemented:

### 6a. Build & Test

```bash
dotnet build DRN.slnx
dotnet test DRN.slnx
```

All must pass. If not → return to §5.

### 6b. Self-Review

- Run `/review` on implemented changes
- Run Priority Stack gate (Security → Correctness → Clarity → Simplicity → Performance); TRIZ for constraint conflicts
- **Clean Code Check** — verify each changed file passes the Clean Code Gate (§5 step 4): SoC, method size, CC ≤ 5, naming, no dead code; confirm any overrides are documented
- Update documentation if needed (`basic-documentation` skill)

---

## 7. Report & Update Status

### 7a. Summary Report

Produce walkthrough artifact:

- **Source document** and **PBIs implemented** (count + IDs)
- **Changes table**: PBI → Files Changed → Tests Added → Status
- **Build & Test results**
- **Priority Stack validation** (pass/fail per gate)
- **Notes**: deviations, decisions, issues

Present to user for final review.

### 7b. Update Document Status (After User Approval)

Pre-update checklist:
- [ ] User approval received for final summary report

Only after user confirms:

```yaml
# Preserve existing fields (title, created, clarified, etc.)
status: implemented
implemented: [ISO 8601 date]
```

> Do **not** set `status: implemented` before user approval.
