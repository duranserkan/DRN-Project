---
description: Implement approved DEVELOP artifacts with DiSCOS, AGENTS.md, and repository guidance
---

> **Pipeline**: `/clarify` -> `/answer` -> `/develop` (3/3) · [Status Lifecycle](./_shared/status-lifecycle.md) · [Operating Model](./_shared/workflow-operating-model.md)
> **Estimated context: ~2.0K tokens**

## 1. Resolve Input

Run Startup Gate once: read `AGENTS.md`, `.agent/rules/DiSCOS.md` when present, `.agent/repository-profile.md` when present, this workflow, and only needed skills.

| Input | Action |
|---|---|
| Explicit `.agent/temp/DEVELOP-*.md` | Use it. |
| Explicit `.agent/temp/CLARIFY-*.md` | Do not implement. Route through `/answer` Section 6 to produce `DEVELOP-*`, then rerun `/develop`. |
| No arguments | Scan `.agent/temp/` for `DEVELOP-*.md`; use one, ask on multiple, or route to `/clarify` then `/answer` when none exists. |
| Inline plan or generic planning file | Stop. Require a current `.agent/temp/DEVELOP-[task-slug].md`. |

Never bypass CAD. `/develop` mutates source only from a valid, current `DEVELOP-*` handoff.

## 2. Validate Handoff

Read YAML `status`:

| Status | Action |
|---|---|
| `ready-to-develop` | Continue. |
| `clarified` | Run `/answer` Section 6 to create `DEVELOP-*`, then rerun `/develop`. |
| `draft-self-reviewed` | Use the CLARIFY Skip Gate only to decide whether `/answer` may skip approval while still creating `DEVELOP-*`. |
| `draft`, `clarifying`, or missing | Stop. Route to `/clarify` plus `/answer`. |
| `implemented` | Warn; resume only after explicit confirmation. |

Do not reopen product strategy by default. Prove the handoff is current, approved, and implementation-ready before edits.

### 2a. CLARIFY Skip Gate

Use only to let `/answer` Section 6 create `DEVELOP-*` without another approval round. It never authorizes direct implementation from `CLARIFY-*`.

Require all:
- No `[ASSUMPTION - unverified]` tags in PBIs.
- Accepted assumptions use `[ASSUMPTION - accepted]` with mitigation.
- Every PBI has acceptance criteria.
- Security implications are addressed.
- In/out-of-scope is unambiguous.
- Explicit user approval or valid `ApprovalRecord=workflow-tolerated` is recorded for the approval skip.

All pass -> run `/answer` Section 6, then rerun `/develop`. Any failure -> redirect to `/answer`.

### 2b. Freshness, Review, Approval

For `DEVELOP-*`, verify `source`, `source_status`, `source_updated`, and `source_sha256` against the source `CLARIFY-*`.

- Source missing, newer, hash-mismatched, or superseded -> set or report `stale: true`; recommend `/answer` Section 6 on the intended latest source.
- `needs_review: true` -> run `/review` before implementation.
- `approval_required: true` -> capture explicit approval before source mutation unless a valid shared `ApprovalRecord=workflow-tolerated` applies.
- Direct `/develop <DEVELOP path>` may satisfy approval only for that exact artifact, bounded scope, and risk; record `approval_record: explicit approval recorded`, `approval_scope`, and `approval_required: false` before edits.
- `/goal` may supply `ApprovalRecord=workflow-tolerated` only within shared lifecycle limits.
- `approval_required: false` without current matching `approval_record` and `approval_scope` -> stop.

Security-sensitive, failed, unclear, destructive, VCS, or non-tolerable gates still require explicit human approval.

### 2c. Completeness Gate

Before planning edits, verify:
- Source metadata matches the clarified `CLARIFY-*`.
- No `[ASSUMPTION - unverified]`, `stale: true`, unresolved `needs_review: true`, unresolved `approval_required: true`, or missing approval record.
- Scope, requirements, PBIs, and acceptance criteria are clear and testable.
- `Implementation Context` names files/context to read, relevant skills, and verification permissions.
- `Lineage Notes` exist when source evidence continues prior artifacts or implementation.
- Expert Lens findings and `/answer` tradeoffs appear in criteria, `Architecture Guidance` constraints, `Risk Register`, or Priority Stack Validation.
- Expert-attributed Q&A labels remain.
- Priority Stack Validation covers Security, Correctness, Clarity, Simplicity, and Performance.

Failure stops implementation. Route to `/answer` for stale metadata, missing handoff data, missing risk/constraint traceability, or resolvable implications. Route to `/clarify` when scope, criteria, or critical assumptions need a human decision.

## 3. Load Context And Skills

Read:
- `.agent/workflows/_shared/workflow-operating-model.md`.
- `.agent/skills/overview-skill-index/SKILL.md`.
- `.agent/skills/basic-agentic-development/SKILL.md` for Autonomy Ladder and Development Loop.
- `DEVELOP-*` sections: Lineage Notes, Risk Register, accepted assumptions, Architecture Guidance, relevant skills, verification permissions, expert findings, tradeoffs, and constraints.

Reuse context loaded by `/clarify` or `/answer`. Load only PBI-needed skills.

| Need | Skills |
|---|---|
| Domain/entity | `overview-ddd-architecture` plus profile domain skills |
| API/hosting | `basic-security-checklist`, `test-integration-api`, profile hosting skills |
| Frontend | Matching frontend skills |
| Testing | Testing profile plus `test-unit`, `test-integration`, `test-integration-api`, `test-integration-db` |
| Infrastructure | `overview-repository-structure`, `overview-github-actions` |
| Docs | `basic-documentation`, `basic-documentation-diagrams` |

Filter backlog by requested EPICs/PBIs. Warn on dependencies. With no filter, implement the full backlog.

## 4. Plan And Preflight

For each PBI, in priority order:
1. Identify affected files.
2. Map concrete tasks.
3. Start with `Risk Register` risks and constraints; add implementation risks.
4. Classify complexity: Trivial, Standard, Significant, or Critical.
5. Stop on `[ASSUMPTION - unverified]`.
6. Resolve conflicts with TRIZ, then Priority Stack.
7. Preserve approved strategy unless discovery proves the handoff stale, contradictory, unsafe, or impossible.

Presentation gate:
- Trivial/Standard: summarize and proceed.
- Significant: require explicit approval or accepted `ApprovalRecord=workflow-tolerated`.
- Critical or security-sensitive: require explicit approval.
- PBIs >=3: maintain a checklist.

Run VCS preflight before edits:
- Inspect branch, dirty state, and upstream/base from profile or Git refs.
- Create a branch only when explicitly requested; base it on the profile integration branch, or on a confirmed hotfix release branch.
- Stop if requested branch creation fails.
- Commit only when requested; use `basic-git-conventions`.
- Push only when explicitly requested and approved.
- Never let `/clarify` or `/answer` create branches or commits.
- Before committing `.agent/temp/` CAD artifacts, verify ignore rules and require explicit tracking choice.

## 5. Execute

Run the Development Loop per PBI:
1. Discover: inspect outlines and target-read existing code.
2. Implement the smallest testable unit using repository conventions.
3. Enforce Clean Code Gate on new or materially touched code.
4. Build only when explicitly allowed.
5. Add or update required tests; run tests only when allowed, unit tests first.
6. On failure, fix and re-verify; stop after 2 attempts and escalate.

## 6. Verify

After all PBIs:
1. Run only allowed build/test commands. If not allowed, report "not run per repo rule" and do not claim pass/fail.
2. Run `/review` on implemented changes.
3. Verify Priority Stack and Clean Code Gate.
4. Update docs when behavior, contracts, or conventions changed.
5. Run `git diff --check` unless blocked.

## 7. Report And Status

Create a walkthrough artifact with:
- Source document and implemented PBIs.
- Changes table: PBI -> files changed -> tests added -> status.
- Build/test results and Priority Stack validation.
- Notes, decisions, and deviations.

Set `status: implemented` and `implemented: [ISO 8601 date]` only after user approval.
