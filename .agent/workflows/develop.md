---
description: Implement requirements from a clarified document using DiSCOS, AGENTS.md and repository skills guidance
---

> **Pipeline**: `/clarify` -> `/answer` -> `/develop` (3/3) · [Status Lifecycle](./_shared/status-lifecycle.md) · [Operating Model](./_shared/workflow-operating-model.md)
> **Estimated context: ~1.4K tokens**

---

## 1. Resolve Input

- **Explicit `DEVELOP-*` path** (e.g., `/develop .agent/temp/DEVELOP-x.md`): Use that file.
- **Explicit `CLARIFY-*` path**: Do not implement directly. Redirect to `/answer` §7 to produce a `DEVELOP-*` artifact with source metadata, then re-run `/develop` on that file. This remains mandatory even when `/clarify` was user-approved or approval-tolerable.
- **No arguments**: Scan `.agent/temp/` for `DEVELOP-*.md`. If single, use it. If multiple, ask. If none or inline only, direct to `/clarify` then `/answer`.
- **Mandatory CAD Artifacts**: You must never implement changes directly from generic system planning files without a valid, current `.agent/temp/DEVELOP-[task-slug].md` handoff artifact. Bypassing `/clarify` or `/answer` to perform quick edits is strictly prohibited.
Apply the shared Startup Gate before implementation planning: read `AGENTS.md`, `.agent/rules/DiSCOS.md` when present, `.agent/repository-profile.md` when present, this workflow, and only needed skills.

---

## 2. Validate Status

`/develop` consumes approved `/answer` outputs; it does not reopen product strategy by default or implement from `CLARIFY-*` directly. Use this section to prove the handoff is current and implementation-ready before mutating source.

Read YAML `status`:
- `ready-to-develop`: Proceed to §3.
- `clarified`: Abort and run `/answer` §7 to produce `.agent/temp/DEVELOP-*.md`, then re-run `/develop` on that file.
- `draft-self-reviewed`: Do not implement directly. Use §2a only to decide whether `/answer` may skip the approval phase while still producing `DEVELOP-*`.
- `draft` / `clarifying` / Missing: Abort; direct to `/clarify` + `/answer`.
- `implemented`: Warn user. Resume only on explicit confirmation.

### 2a. `.agent/temp/CLARIFY-*.md` Skip Gate
Skipping the `/answer` approval phase requires explicit user confirmation or `ApprovalRecord=workflow-tolerated` from an allowed producer such as `/goal cad` under the shared approval-record rules. This gate never authorizes direct implementation from `CLARIFY-*`; it only authorizes `/answer` §7 to produce `DEVELOP-*` without another approval round.

Verify:
- [ ] No `[ASSUMPTION - unverified]` tags in PBIs.
- [ ] Accepted assumptions are tagged `[ASSUMPTION - accepted]` and have mitigation.
- [ ] Every PBI has acceptance criteria.
- [ ] Security implications are addressed.
- [ ] In/out-of-scope is unambiguous.
- [ ] Explicit user approval or valid `ApprovalRecord=workflow-tolerated` from an allowed producer such as `/goal cad` to skip the `/answer` approval phase is recorded.
All pass -> run `/answer` §7 to create `DEVELOP-*`, then re-run `/develop` on that file. Any failure -> redirect to `/answer`.

### 2b. Staleness Check

If input is `.agent/temp/DEVELOP-*.md`, verify `source`, `source_status`, `source_updated`, and `source_sha256` against the source `.agent/temp/CLARIFY-*.md`.
- Source missing, newer, or hash mismatch -> set or report `stale: true`; recommend re-running `/answer` §7.
- `needs_review: true` -> run `/review` before implementation.
- `approval_required: true` -> obtain and record explicit approval before mutating unless invoked by a workflow with a valid shared `ApprovalRecord=workflow-tolerated` approval record that this gate accepts. A direct user invocation of `/develop <this DEVELOP path>` may satisfy this gate only for the exact artifact, bounded scope, and risk; record `approval_record: explicit approval recorded`, `approval_scope`, and `approval_required: false` before source edits.
  - Caller exception: `/goal` may produce `ApprovalRecord=workflow-tolerated` for this workflow-local approval only under the shared lifecycle's limits. Failed, unclear, critical, destructive, VCS, security-sensitive, or otherwise non-tolerable gates still require explicit human approval.
- `approval_required: false` without current matching `approval_record` and `approval_scope` -> treat as an unresolved approval gate and stop before mutation.

### 2c. Handoff Completeness Gate

Before loading implementation skills or planning edits, verify the `DEVELOP-*` artifact contains current handoff data from `/answer`:

- Source metadata exists, matches the source clarification document, and `source_status: clarified`.
- No `[ASSUMPTION - unverified]`, `stale: true`, unresolved `needs_review: true`, unresolved `approval_required: true`, or missing approval record when `approval_required: false`.
- Scope, requirements, PBIs, and acceptance criteria are clear and testable.
- `Implementation Context` lists context/files to read, relevant skills, and verification permissions.
- `Architecture Guidance`, requirements, acceptance criteria, `Risk Register`, and `Priority Stack Validation` collectively preserve every relevant Expert Lens Pass finding and answer tradeoff from `/answer`, not only named constraint buckets.
- Confirm the selected Expert Lens Pass categories, including any context-specific lenses, are traceably present before proceeding.
- `Priority Stack Validation` reflects Security, Correctness, Clarity, Simplicity, and Performance.

Any failure stops implementation. Redirect to `/answer` for stale source metadata, missing handoff data, missing risk/constraint traceability, or unresolved implications that can be answered from existing clarification context. Redirect to `/clarify` when scope, acceptance criteria, or critical assumptions require a new human decision.

---

## 3. Load Context & Skills

1. **Read Guidance**:
   - Read file: `AGENTS.md`, `.agent/rules/DiSCOS.md`, `.agent/repository-profile.md`
   - Read file: `.agent/workflows/_shared/workflow-operating-model.md`
   - `.agent/skills/overview-skill-index/SKILL.md`
   - `.agent/skills/basic-agentic-development/SKILL.md` (Autonomy Ladder + Development Loop)
   *Note: Reuse loaded context if `/clarify` or `/answer` ran in the same session.*
   Also read the `DEVELOP-*` sections that `/answer` produced: `Risk Register`, accepted assumptions and mitigations, `Architecture Guidance`, relevant skills, verification permissions, and the complete set of relevant Expert Lens Pass findings, answer tradeoffs, and implementation constraints.
2. **Load Relevant Skills**: Use skill index to load **only** what PBIs need:
   - *Domain/Entity*: `overview-ddd-architecture` + profile-declared domain skills.
   - *API/Hosting*: `basic-security-checklist`, `test-integration-api` + profile hosting skills.
   - *Frontend*: matching frontend skills.
   - *Testing*: testing profile + `test-unit`, `test-integration`, `test-integration-api`, `test-integration-db`.
   - *Infrastructure*: `overview-repository-structure`, `overview-github-actions`.
   - *Docs*: `basic-documentation`, `basic-documentation-diagrams`.
3. **Scope Filtering**: If EPICs/PBIs are specified as arguments, filter the backlog. Warn on dependencies. Otherwise, implement entire backlog.

---

## 4. Plan Implementation

For each PBI (in priority order):
1. **Identify**: Affected existing/new files.
2. **Map**: Concrete tasks.
3. **Identify Risks**: Start from the `Risk Register` and constraints handoff, then add implementation-specific risks such as breaking changes, security, schema changes, or verification limits.
4. **Estimate Complexity**: Trivial / Standard / Significant / Critical.
5. **Assumption Check**: Halt and escalate if any `[ASSUMPTION - unverified]` is found.
6. **Conflicts**: Apply **TRIZ** first, then **Priority Stack**.
7. **Strategy Boundary**: Do not reopen approved product decisions unless §2c fails or implementation discovery proves the handoff is stale, contradictory, unsafe, or impossible.
*Presentation*: Trivial/Standard (summarize and proceed); Significant (proceed only with explicit approval or an accepted `ApprovalRecord=workflow-tolerated`); Critical or security-sensitive (wait for explicit approval). Maintain a checklist if PBIs >= 3.

### 4a. Version Control Setup

Run a VCS preflight before edits:
- Inspect current branch, dirty state, and upstream/base branch from the repository profile or discovered Git refs.
- If the user explicitly requested branch creation, create it from the profile-declared integration branch, or from the release branch only for confirmed hotfixes.
- If branch creation fails, stop and ask; do not silently continue on the current branch.
- Commits are opt-in. When requested, commit per approved checkpoint using `basic-git-conventions`. Never push unless explicitly requested and approved.

---

## 5. Execute

Follow the Development Loop per PBI:
1. **Discovery**: Outline and target-read existing code.
2. **Implement**: Smallest testable unit first, using conventions.
3. **Clean Code Gate** (enforce for new or materially touched code before next PBI).
4. **Validate by Review**: Run build only when explicitly allowed by user.
5. **Tests**:
   - Add or update tests when required by the PBI and repository conventions.
   - Run test commands only when explicitly allowed (unit tests first).
   - Failures -> Self-Correction Loop.

### Self-Correction Loop

Fix failing builds or tests and re-verify. Limit to 2 attempts before escalating.

---

## 6. Verify

After all PBIs are implemented:
1. **Build & Test**: Run only if allowed by user.
   - `<build command>` · `<unit test command>` · `<integration test command>`
   - If not allowed, report "not run per repo rule" (do not claim pass/fail).
2. **Self-Review**:
   - Run `/review` on implemented changes.
   - Verify Priority Stack (Security → Correctness → Clarity → Simplicity → Performance) and Clean Code Gate.
   - Update documentation if needed.
3. **Whitespace/patch check**: Run `git diff --check` unless blocked.

---

## 7. Report & Update Status

1. **Walkthrough Report**: Create walkthrough artifact containing:
   - Source document and implemented PBIs.
   - Changes table (PBI → Files Changed → Tests Added → Status).
   - Build/test results and Priority Stack validation.
   - Notes, decisions, and deviations.
2. **Update Status**: Only after user approval, set `status: implemented` and `implemented: [ISO 8601 date]`.
