---
description: Implement requirements from a clarified document using DiSCOS, AGENTS.md and repository skills guidance
---

> **Pipeline**: `/clarify` → `/answer` → `/develop` (3/3) · [Status Lifecycle](./_shared/status-lifecycle.md)
> **Estimated context: ~1.4K tokens**

---

## 1. Resolve Input
- **Explicit path** (e.g., `/develop DEVELOP-x.md` or `CLARIFY-x.md`): Use that file.
- **No arguments**: Scan root for `DEVELOP-*.md`, then `CLARIFY-*.md`. If single, use it. If multiple, ask. If none or inline only, direct to `/clarify` then `/answer`.

---

## 2. Validate Status
Read YAML `status`:
- `ready-to-develop`: Proceed to §3.
- `clarified`: Abort and run `/answer` §7 to produce `DEVELOP-*.md`, then re-run `/develop` on that file.
- `draft-self-reviewed`: Proceed only via §2a gate (skip `/answer` path).
- `draft` / `clarifying` / Missing: Abort; direct to `/clarify` + `/answer`.
- `implemented`: Warn user. Resume only on explicit confirmation.

### 2a. CLARIFY-*.md Lightweight Gate (if skipping `/answer`)
Verify:
- [ ] No `[ASSUMPTION - unverified]` tags in PBIs.
- [ ] Every PBI has acceptance criteria.
- [ ] Security implications are addressed.
All pass → proceed to §3. Any failure → redirect to `/answer`.

### 2b. Staleness Check
If input is `DEVELOP-*.md`, compare modification date with source `CLARIFY-*.md`. If source is newer, warn user and recommend re-running `/answer` §7.

---

## 3. Load Context & Skills
1. **Read Guidance**:
   - `view_file AGENTS.md` · `view_file .agent/repository-profile.md`
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
- If already on task branch, use it. Otherwise, checkout `feature/[task-name-or-id] develop` (or `fix/[id] master` for hotfixes).
- If branch creation fails (permissions, protected, read-only), notify user and **continue on current branch**.
- Commit per PBI using conventional commits (`feat(Scope): desc`). **Do not push.**

---

## 5. Execute
Follow the Development Loop per PBI:
1. **Discovery**: Outline and target-read existing code.
2. **Implement**: Smallest testable unit first, using conventions.
3. **Clean Code Gate** (enforce before next PBI):
   - *Separation of concerns*: No business logic in controllers/handlers; no persistence logic in domain.
   - *Method size*: Extract methods > 20 lines or doing > 1 thing.
   - *Cyclomatic complexity (CC)*: Max CC 5 per method (document inline if overridden).
   - *Naming*: Express intent; no abbreviations or generic names (`data`, `result`).
   - *Dead code*: Remove commented-out code, unused parameters, unreachable branches.
4. **Validate**: Run build only when explicitly allowed by user.
   - Build fails → Self-Correction Loop. Toolchain missing/blocked → notify user, skip validation, continue.
5. **Write/Run Tests**: Pure logic (Unit), Persistence (Integration with Testcontainers), API (API Integration). Run only when explicitly allowed (unit tests first).
   - Failures → Self-Correction Loop.

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

---

## 7. Report & Update Status
1. **Walkthrough Report**: Create walkthrough artifact containing:
   - Source document and implemented PBIs.
   - Changes table (PBI → Files Changed → Tests Added → Status).
   - Build/test results and Priority Stack validation.
   - Notes, decisions, and deviations.
2. **Update Status**: Only after user approval, set `status: implemented` and `implemented: [ISO 8601 date]`.
