---
description: Implement requirements from a clarified document using DiSCOS, AGENTS.md and repository skills guidance
---

> **Pipeline**: `/clarify` -> `/answer` -> `/develop` (3/3) · [Status Lifecycle](./_shared/status-lifecycle.md) · [Operating Model](./_shared/workflow-operating-model.md)
> **Estimated context: ~1.4K tokens**

---

## 1. Resolve Input

- **Explicit `DEVELOP-*` path** (e.g., `/develop .agent/temp/DEVELOP-x.md`): Use that file.
- **Explicit `CLARIFY-*` path**: Do not implement directly. Redirect to `/answer` §7 to produce a `DEVELOP-*` artifact with source metadata, then re-run `/develop` on that file.
- **No arguments**: Scan `.agent/temp/` for `DEVELOP-*.md`. If single, use it. If multiple, ask. If none or inline only, direct to `/clarify` then `/answer`.
Apply the shared Startup Gate before implementation planning: read `AGENTS.md`, `.agent/rules/DiSCOS.md` when present, `.agent/repository-profile.md` when present, this workflow, and only needed skills.

---

## 2. Validate Status

Read YAML `status`:
- `ready-to-develop`: Proceed to §3.
- `clarified`: Abort and run `/answer` §7 to produce `.agent/temp/DEVELOP-*.md`, then re-run `/develop` on that file.
- `draft-self-reviewed`: Do not implement directly. Use §2a only to decide whether `/answer` may skip the approval phase while still producing `DEVELOP-*`.
- `draft` / `clarifying` / Missing: Abort; direct to `/clarify` + `/answer`.
- `implemented`: Warn user. Resume only on explicit confirmation.

### 2a. `.agent/temp/CLARIFY-*.md` Skip Gate
Skipping the `/answer` approval phase requires explicit user confirmation. This gate never authorizes direct implementation from `CLARIFY-*`; it only authorizes `/answer` §7 to produce `DEVELOP-*` without another approval round.

Verify:
- [ ] No `[ASSUMPTION - unverified]` tags in PBIs.
- [ ] Accepted assumptions are tagged `[ASSUMPTION - accepted]` and have mitigation.
- [ ] Every PBI has acceptance criteria.
- [ ] Security implications are addressed.
- [ ] In/out-of-scope is unambiguous.
- [ ] User approval to skip the `/answer` approval phase is recorded.
All pass -> run `/answer` §7 to create `DEVELOP-*`, then re-run `/develop` on that file. Any failure -> redirect to `/answer`.

### 2b. Staleness Check

If input is `.agent/temp/DEVELOP-*.md`, verify `source`, `source_status`, `source_updated`, and `source_sha256` against the source `.agent/temp/CLARIFY-*.md`.
- Source missing, newer, or hash mismatch -> set or report `stale: true`; recommend re-running `/answer` §7.
- `needs_review: true` -> run `/review` before implementation.
- `approval_required: true` -> obtain explicit approval before mutating.

---

## 3. Load Context & Skills

1. **Read Guidance**:
   - Read file: `AGENTS.md`, `.agent/rules/DiSCOS.md`, `.agent/repository-profile.md`
   - Read file: `.agent/workflows/_shared/workflow-operating-model.md`
   - `.agent/skills/overview-skill-index/SKILL.md`
   - `.agent/skills/basic-agentic-development/SKILL.md` (Autonomy Ladder + Development Loop)
   *Note: Reuse loaded context if `/clarify` or `/answer` ran in the same session.*
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
3. **Identify Risks**: Breaking changes, security, schema changes.
4. **Estimate Complexity**: Trivial / Standard / Significant / Critical.
5. **Assumption Check**: Halt and escalate if any `[ASSUMPTION - unverified]` is found.
6. **Conflicts**: Apply **TRIZ** first, then **Priority Stack**.
*Presentation*: Trivial/Standard (summarize and proceed); Significant/Critical (wait for explicit approval). Maintain a checklist if PBIs ≥ 3.

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
3. **Clean Code Gate** (enforce for new or materially touched code before next PBI):
   - *Separation of concerns*: No business logic in controllers/handlers; no persistence logic in domain.
   - *Method size*: Extract methods > 20 lines or doing > 1 thing when it improves clarity.
   - *Cyclomatic complexity (CC)*: Max CC 5 per touched method unless local patterns justify a documented exception.
   - *Naming*: Express intent; no abbreviations or generic names (`data`, `result`).
   - *Dead code*: Remove commented-out code, unused parameters, unreachable branches.
4. **Validate**: Run build only when explicitly allowed by user.
   - Build fails -> Self-Correction Loop.
   - Toolchain missing/blocked -> mark verification blocked; continue only for low-risk static work and never imply pass/fail.
5. **Tests**:
   - Add or update tests when required by the PBI and repository conventions.
   - Run test commands only when explicitly allowed (unit tests first).
   - Failures -> Self-Correction Loop.

### Self-Correction Loop

Build/test fails → Fix → Re-verify. Stop after **2 failed attempts** on the same issue; report errors, hypotheses, and proposed next steps.

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
