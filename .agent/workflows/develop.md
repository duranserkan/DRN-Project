---
description: Implement requirements from a clarified document using repository skills and AGENTS.md guidance
---

## Table of Contents

- [1. Resolve Input](#1-resolve-input)
- [2. Validate Status](#2-validate-status)
- [3. Load Context & Skills](#3-load-context--skills)
- [4. Plan Implementation](#4-plan-implementation)
- [5. Execute](#5-execute)
- [6. Verify](#6-verify)
- [7. Report & Update Status](#7-report--update-status)

---

## 1. Resolve Input

Determine what to implement:

- **Explicit path** (e.g., `/develop CLARIFY-user-preferences.md`) → Use that file.
- **No arguments** → Scan the repository root for `CLARIFY-*.md` files.
  - **One found** → Use it.
  - **Multiple found** → List them and ask the user which one to implement.
  - **None found** → Stop. Inform the user:
    > No clarified requirements found. Run `/clarify [task description]` first to produce a structured requirements document.
- **Inline requirements without a clarify doc** → Stop. Guide the user:
  > To ensure thorough requirements analysis, run `/clarify [your task]` first. It will produce a structured, validated document that `/develop` can implement.

---

## 2. Validate Status

Read the YAML frontmatter of the resolved file. Check `status`:

| Status | Action |
|---|---|
| `clarified` | ✅ Proceed to Step 3 |
| `draft` | ❌ Stop. Inform: *"This document is still in draft. Complete the `/clarify` workflow first — the status must be `clarified` before development can begin."* |
| `implemented` | ⚠️ Warn: *"This document was already implemented. Proceed anyway?"* Continue only if user confirms. |
| Missing/other | ❌ Stop. Inform: *"This file does not have a valid clarify status. Run `/clarify` to produce a properly structured document."* |

---

## 3. Load Context & Skills

### 3a. Read Project Guidance

- `view_file AGENTS.md` — behavioral framework, project overview, conventions
- `view_file .agent/skills/overview-skill-index/SKILL.md` — task→skill lookup
- `view_file .agent/skills/basic-agentic-development/SKILL.md` — Autonomy Ladder and Development Loop (referenced in §4 and §5)

### 3b. Select & Load Relevant Skills

Using the skill index, identify which skills are relevant to the PBIs in the clarify document. Load **only** what's needed (Context Economy principle).

| PBI Domain | Likely Skills |
|---|---|
| Domain/entity design | `drn-domain-design`, `drn-sharedkernel`, `drn-entityframework` |
| API/hosting | `drn-hosting`, `basic-security-checklist` |
| Frontend/UI | `frontend-*` skills |
| Testing | `test-unit`, `test-integration`, `test-integration-api`, `test-integration-db` |
| Infrastructure | `overview-repository-structure`, `overview-github-actions` |

### 3c. Scope Selection (Optional)

If the user provided scope arguments (e.g., `/develop CLARIFY-x.md EPIC-001` or `/develop CLARIFY-x.md PBI-001 PBI-002`):
- Filter the backlog to only the specified EPICs or PBIs.
- Resolve dependencies: if a selected PBI depends on unselected PBIs, warn the user.

If no scope is specified, implement the **entire backlog** in priority order.

---

## 4. Plan Implementation

For each PBI (in dependency and priority order):

1. **Identify affected files** — existing files to modify, new files to create.
2. **Map to implementation tasks** — concrete code changes, not abstract descriptions.
3. **Identify risks** — breaking changes, security implications, schema changes.
4. **Estimate complexity** — trivial / standard / significant / critical (per Autonomy Ladder).
5. **Assumption Check** — Scan the PBI for any `[ASSUMPTION - unverified]` tags. If found, explicitly ask the user for clarification on those specific points before proceeding to execution.

Produce an implementation plan artifact and present to the user for approval:
- **Trivial/Standard** PBIs → Summarize briefly, proceed after presenting.
- **Significant/Critical** PBIs → Detailed plan, wait for explicit user approval.

> **DiSCOS Autonomy Ladder**: Read the `basic-agentic-development` skill to understand when to act vs. ask.

### 4a. Version Control Setup

- **Branch guard**: Check the current branch first (`git branch --show-current`). If already on a branch dedicated to this task (e.g., `feature/[task-name-or-id]`), skip branching and use the current branch.
- **Otherwise**, branch off `develop` to create a dedicated feature branch: `git checkout -b feature/[task-name-or-id] develop`.
- As you complete each PBI (or logical chunk), commit the changes locally using conventional commits (e.g., `feat(Scope): description`).
- **Do not push** the branch. Local commits serve as checkpoints for safe rollbacks.

---

## 5. Execute

Follow the Development Loop from `basic-agentic-development`:

### For each PBI (in planned order):

1. **Discovery** — Read existing code in affected areas (`view_file_outline` first, then targeted reads).
2. **Implement** — Make changes incrementally, smallest testable unit first.
3. **Follow conventions** — Use existing patterns from the codebase:
   - Attribute-based DI (`[Scoped]`, `[Singleton]`, etc.)
   - Source-Known ID pattern for entities
   - Existing namespace conventions (verify with `grep_search`, never assume)
4. **Validate after each change**:

   ```bash
   dotnet build DRN.slnx    # Must pass before proceeding
   ```

5. **Write tests** — Apply DTT philosophy (load testing skills if not already loaded):
   - Pure logic → Unit tests
   - Persistence/queries → Integration tests with Testcontainers
   - API endpoints → API integration tests
6. **Run tests**:

   ```bash
   dotnet test DRN.slnx     # Must pass before proceeding
   ```


### Self-Correction Loop

- ❌ Build/test fails → Read error → Fix → Re-verify → Continue
- ⚠️ After **2 failed attempts** on the same issue → Stop. Report to user: what was tried, what failed, hypotheses, recommended next steps.

---

## 6. Verify

After all PBIs are implemented:

### 6a. Build & Test

```bash
dotnet build DRN.slnx
dotnet test DRN.slnx
```

All must pass. If not, return to Step 5 for the failing area.

### 6b. Priority Stack Self-Review

| Gate | Question |
|---|---|
| **Security** | Are security requirements from the clarify doc fully addressed? Any new attack surface? |
| **Correctness** | Does each PBI's acceptance criteria pass? Any edge cases missed? |
| **Clarity** | Is the code readable and self-documenting? Comments where non-obvious? |
| **Simplicity** | Could the implementation be simpler without losing correctness? Over-engineered? |
| **Performance** | Any hot paths introduced? Are queries efficient? |

### 6c. Documentation

- Update documentation if changes require it (README, API docs, skill files).
- Follow `basic-documentation` skill guidelines.

---

## 7. Report & Update Status

### 7a. Summary Report

Produce a summary (as a walkthrough artifact if in task mode):

```markdown
## Development Summary

**Clarify Document**: [filename]
**PBIs Implemented**: [count]

### Changes Made

| PBI | Files Changed | Tests Added | Status |
|---|---|---|---|
| PBI-001 | file1.cs, file2.cs | TestClass.Method | ✅ Done |

### Build & Test Results

- Build: ✅ Pass
- Tests: ✅ [X] passed, [Y] skipped, [Z] failed

### Priority Stack Validation

- Security: ✅/❌
- Correctness: ✅/❌
- Clarity: ✅/❌
- Simplicity: ✅/❌
- Performance: ✅/❌

### Notes

- [Any deviations from plan, decisions made, or issues encountered]
```

Present the summary to the user for final review.

### 7b. Update Clarify Document (After User Approval)

Only after the user confirms the summary, update the YAML frontmatter of the clarify document:

```yaml
---
status: implemented
title: [unchanged]
created: [unchanged]
clarified: [unchanged]
implemented: [ISO 8601 date]
---
```

> Do **not** set `status: implemented` before user approval — mirrors the `/clarify` pattern where `status: clarified` is set only after user confirms.
