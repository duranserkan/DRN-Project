---
description: Add tests for staged changes or a described task using repository conventions
---

> **Estimated context: ~0.8K tokens** + testing skills.
> See also: [Operating Model](./_shared/workflow-operating-model.md)
> Do not run tests unless the user or profile explicitly allows it.

## 1. Startup

Run the shared Startup Gate once. Load only needed testing skills.

## 2. Scope

- **No arguments**: inspect staged changes with `git diff --cached`; if empty, report and stop.
- **Task/description**: map source files from keywords or git history; read minimal context.
- **Mutation boundary**: add or update tests only. If source changes are required for meaningful tests, report why and wait for approval.

## 3. Guidance

Load profile-specific rules before generic test skills:
- Read `.agent/repository-profile.md`.
- Load `.agent/workflows/load-skills-test.md` when repository testing conventions may apply.
- For narrow tasks, load only classifier-relevant test skills after reading the profile.
- Load performance guidance only when the user asks for performance coverage.

## 4. Classify

| Change | Test type |
|---|---|
| Deterministic business rule, utility, branch | Unit |
| Service wiring without external dependencies | Unit or component |
| Persistence, repository, ORM, SQL, concurrency | DB integration |
| Endpoint, controller, auth, middleware, serialization | API integration |
| Bug fix | Regression test at affected layer |
| Performance-sensitive path | Benchmark only on explicit request |

Use framework attributes and contexts only when the profile or testing skills declare them.

## 5. Plan

- Extend existing test classes when possible.
- Parameterize identical bodies with the local test framework data attributes.
- Prefer in-process DB integration over full API/E2E unless the web stack is under test.
- Keep tests separate when consolidation hides distinct behavior or failure modes.
- For staged scope, cite the changed file or diff hunk each test covers.

## 6. Write And Verify

- Write tests by repository conventions.
- If test execution is allowed, run unit tests first; discover commands from profile, CI, or files.
- If tests are not allowed, perform static verification: reread touched test sections, confirm attributes/contexts match loaded guidance, and report build/test as `not run per repo rule`.
- Run `git diff --check` after edits unless blocked.
- For blocked or skipped verification, use the [Evidence Contract](./_shared/workflow-operating-model.md): Evidence, Impact, Invariant, Recommendation, Confidence, Verification.

## 7. Self-Review

Apply Priority Stack:
- Security: fixtures, generated data, and logs must not leak secrets.
- Correctness: tests assert behavior, not mock plumbing.
- Clarity: tests read clearly without source spelunking.
- Simplicity: fewer tests may cover the behavior only when failure modes stay visible.
- Performance: runtime cost must match signal.

Report residual risk with the Evidence Contract when verification is blocked or incomplete.
