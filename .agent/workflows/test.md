---
description: Add tests for staged changes or a described task using repository conventions. Use DiSCOS, AGENTS.md, repository profile, and relevant testing skills.
---

> **Estimated context: ~0.6K tokens** (this workflow) + testing skills. Do not run tests unless explicitly allowed by user/profile.
> See also: [Operating Model](./_shared/workflow-operating-model.md)

---

## 1. Startup Gate
Apply the shared Startup Gate before work: read `AGENTS.md`, `.agent/rules/DiSCOS.md` when present, `.agent/repository-profile.md` when present, this workflow, the shared operating model, and only needed testing skills.

---

## 2. Determine Scope
- **No arguments**: Test staged changes (`git diff --cached`). If empty, inform user and stop.
- **Task/description**: Identify source files via keywords/git history; read minimal context.
- **Mutation boundary**: Add or update tests only. Do not modify staged/source files unless the requested task requires it; if source changes are needed to make tests meaningful, report the reason and wait for approval.

---

## 3. Load Testing Guidance
Load profile-specific guidance first, then target-specific generic skills:
- Read file: `.agent/repository-profile.md`
- Load `.agent/workflows/load-skills-test.md` when the scope may need repository testing conventions.
- For narrow tasks, load only the classifier-relevant testing skills after reading the profile.
- Load performance guidance only when the user explicitly asks for performance coverage.

---

## 4. Classify
| Change | Test Type |
|---|---|
| Pure business rule, utility, deterministic branch | Unit |
| Service wiring without external dependencies | Unit or component test |
| Entity persistence, repository, ORM mapping, SQL, concurrency | DB integration |
| Endpoint/controller/auth/middleware/serialization | API integration |
| Bug fix | Regression test matching affected layer |
| Performance-sensitive path | Benchmark (explicit request only) |

*Note: Use framework attributes and contexts only if declared in profile or testing skills.*

---

## 5. Plan Consolidation-First
- Extend existing test classes where possible.
- Parameterize identical bodies with test framework data attributes.
- Prefer in-process DB integration over full API/E2E if web stack is not under test.
- Keep tests separate if combining obscures different behaviors/failures.
- For staged-change scope, cite the changed file or diff hunk that each test covers.

---

## 6. Write and Verify
- Write tests per repository conventions.
- Build/test commands are permission-gated. If not explicitly allowed, do not run; report skipped.
- If allowed, run unit tests first. Discover commands from CI/files if profile is silent.
- Run `git diff --check` after edits unless blocked.
- If tests are not allowed, perform static verification: read the touched test file sections, confirm attributes/contexts match loaded guidance, and report build/test as `not run per repo rule`.

---

## 7. Self-Review
Apply Priority Stack:
- **Security**: Could fixtures, generated data, or logs leak secrets?
- **Correctness**: Does it assert meaningful behavior rather than mock plumbing?
- **Clarity**: Is the test readable without reading the source?
- **Simplicity**: Can fewer tests cover the behavior without hiding failure modes?
- **Performance**: Is runtime justified by signal?

Report findings or residual risk using the shared Evidence Contract when verification is blocked or incomplete.
