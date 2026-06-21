---
description: Implement approved DEVELOP artifacts with DiSCOS, AGENTS.md, and repository guidance
---

> **Pipeline**: `/clarify` -> `/answer` -> `/develop` (3/3) · [Status Lifecycle](./_shared/status-lifecycle.md) · [Operating Model](./_shared/workflow-operating-model.md)
> **Estimated context: ~2.1K tokens**

---

## 1. Resolve Input

Run the shared Startup Gate once: read `AGENTS.md`, `.agent/rules/DiSCOS.md` when present, `.agent/repository-profile.md` when present, this workflow, and only needed skills.

Resolve arguments:

| Input | Action |
|---|---|
| Explicit `.agent/temp/DEVELOP-*.md` | Use it. |
| Explicit `.agent/temp/CLARIFY-*.md` | Do not implement, even when user-approved or approval-tolerable. Route through `/answer` §7 to produce `DEVELOP-*`, then re-run `/develop`. |
| No arguments | Scan `.agent/temp/` for `DEVELOP-*.md`; use the single match, ask on multiple matches, or route to `/clarify` then `/answer` when none exists. |
| Inline plan or generic planning file | Stop. Require a valid current `.agent/temp/DEVELOP-[task-slug].md`. |

Never bypass CAD artifacts. `/develop` implements only a valid, current `DEVELOP-*` handoff.

---

## 2. Validate Handoff

Read YAML `status`:

| Status | Action |
|---|---|
| `ready-to-develop` | Continue. |
| `clarified` | Run `/answer` §7 to create `DEVELOP-*`, then re-run `/develop`. |
| `draft-self-reviewed` | Use §2a only to decide whether `/answer` may skip approval while still creating `DEVELOP-*`. |
| `draft`, `clarifying`, or missing | Stop. Route to `/clarify` + `/answer`. |
| `implemented` | Warn user; resume only after explicit confirmation. |

Do not reopen product strategy by default. Prove current, approved, implementation-ready handoff before mutating source.

### 2a. CLARIFY Skip Gate

Use this gate only to let `/answer` §7 create `DEVELOP-*` without another approval round. It never authorizes direct implementation from `CLARIFY-*`.

Require all:

- No `[ASSUMPTION - unverified]` tags in PBIs.
- Accepted assumptions use `[ASSUMPTION - accepted]` and include mitigation.
- Every PBI has acceptance criteria.
- Security implications are addressed.
- In/out-of-scope is unambiguous.
- Explicit user approval or valid `ApprovalRecord=workflow-tolerated` from an allowed producer such as `/goal cad` is recorded for the approval skip.

All pass -> run `/answer` §7, then re-run `/develop`. Any failure -> redirect to `/answer`.

### 2b. DEVELOP Freshness And Approval

For `DEVELOP-*`, verify `source`, `source_status`, `source_updated`, and `source_sha256` against the source `CLARIFY-*`.

- Source missing, newer, hash-mismatched, or superseded -> set or report `stale: true`; recommend `/answer` §7 on the intended latest source.
- `needs_review: true` -> run `/review` before implementation.
- `approval_required: true` -> record explicit approval before source mutation unless this workflow accepts a valid shared `ApprovalRecord=workflow-tolerated`.
- Direct `/develop <DEVELOP path>` may satisfy approval only for that exact artifact, bounded scope, and risk; record `approval_record: explicit approval recorded`, `approval_scope`, and `approval_required: false` before edits.
- `/goal` may supply `ApprovalRecord=workflow-tolerated` only within shared lifecycle limits. Failed, unclear, critical, destructive, VCS, security-sensitive, or non-tolerable gates still require explicit human approval.
- `approval_required: false` without current matching `approval_record` and `approval_scope` -> stop.

### 2c. Completeness Gate

Before planning edits, verify the `DEVELOP-*` contains:

- Source metadata that matches `CLARIFY-*` and `source_status: clarified`.
- No `[ASSUMPTION - unverified]`, `stale: true`, unresolved `needs_review: true`, unresolved `approval_required: true`, or missing approval record when approval is cleared.
- Clear, testable scope, requirements, PBIs, and acceptance criteria.
- `Implementation Context` with files/context to read, relevant skills, and verification permissions.
- `Lineage Notes` when the source continues prior artifacts or contains enriched lineage evidence: prior clarify/develop/implementation evidence, carried-forward decisions, superseded decisions, iteration delta, and unresolved follow-ups converted to risks or PBIs.
- Expert Lens findings and `/answer` tradeoffs routed into acceptance criteria, `Architecture Guidance` -> `Constraints`, `Risk Register`, or `Priority Stack Validation`.
- Expert-attributed questions or answers still labeled.
- `Priority Stack Validation`: Security, Correctness, Clarity, Simplicity, Performance.

Failure stops implementation. Route to `/answer` for stale metadata, missing handoff data, missing risk/constraint traceability, or resolvable implications. Route to `/clarify` when scope, acceptance criteria, or critical assumptions need a new human decision.

---

## 3. Load Context And Skills

Read:

- `.agent/workflows/_shared/workflow-operating-model.md`.
- `.agent/skills/overview-skill-index/SKILL.md`.
- `.agent/skills/basic-agentic-development/SKILL.md` for Autonomy Ladder and Development Loop.
- `DEVELOP-*` sections from `/answer`: `Lineage Notes`, `Risk Register`, accepted assumptions and mitigations, `Architecture Guidance`, relevant skills, verification permissions, Expert Lens findings, answer tradeoffs, and implementation constraints.

Reuse context already loaded by `/clarify` or `/answer` in this session.

Load only PBI-needed skills:

| Need | Skills |
|---|---|
| Domain/entity | `overview-ddd-architecture` + profile-declared domain skills |
| API/hosting | `basic-security-checklist`, `test-integration-api` + profile hosting skills |
| Frontend | Matching frontend skills |
| Testing | Testing profile + `test-unit`, `test-integration`, `test-integration-api`, `test-integration-db` |
| Infrastructure | `overview-repository-structure`, `overview-github-actions` |
| Docs | `basic-documentation`, `basic-documentation-diagrams` |

Filter backlog by requested EPICs/PBIs. Warn on dependencies. With no filter, implement the full backlog.

---

## 4. Plan And Preflight

For each PBI, in priority order:

1. Identify affected files.
2. Map concrete tasks.
3. Start risks from `Risk Register` and constraints; add implementation risks.
4. Classify complexity: Trivial, Standard, Significant, or Critical.
5. Stop on any `[ASSUMPTION - unverified]`.
6. Resolve conflicts with TRIZ, then Priority Stack.
7. Preserve approved strategy unless §2c fails or discovery proves the handoff stale, contradictory, unsafe, or impossible.

Presentation gate: summarize and proceed for Trivial/Standard. Require explicit approval or accepted `ApprovalRecord=workflow-tolerated` for Significant. Require explicit approval for Critical or security-sensitive work. Maintain a checklist when PBIs >= 3.

Run VCS preflight before edits:

- Inspect branch, dirty state, and upstream/base from profile or Git refs.
- Create a branch only when explicitly requested; base it on the profile integration branch, or on a release branch only for confirmed hotfixes.
- Stop if requested branch creation fails.
- Commit only when requested; use `basic-git-conventions`.
- Push only when explicitly requested and approved.
- Never let `/clarify` or `/answer` create branches or commits.
- Before committing `.agent/temp/` CAD artifacts, verify ignore rules and require explicit tracking choice.

---

## 5. Execute

Run the Development Loop per PBI:

1. Discover: inspect outlines and target-read existing code.
2. Implement the smallest testable unit using repository conventions.
3. Enforce Clean Code Gate on new or materially touched code.
4. Build only when explicitly allowed.
5. Add or update required tests; run tests only when allowed, unit tests first.
6. On failure, fix and re-verify; stop after 2 attempts and escalate.

---

## 6. Verify

After all PBIs:

1. Run allowed build/test commands only. If not allowed, report "not run per repo rule" and do not claim pass/fail.
2. Run `/review` on implemented changes.
3. Verify Priority Stack and Clean Code Gate.
4. Update documentation when behavior, contracts, or conventions changed.
5. Run `git diff --check` unless blocked.

---

## 7. Report And Update Status

Create a walkthrough artifact with:

- Source document and implemented PBIs.
- Changes table: PBI -> files changed -> tests added -> status.
- Build/test results and Priority Stack validation.
- Notes, decisions, and deviations.

Set `status: implemented` and `implemented: [ISO 8601 date]` only after user approval.
