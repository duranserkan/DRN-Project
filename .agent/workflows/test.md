---
description: Add tests for staged changes or a described task using repository conventions. Use DiSCOS, AGENTS.md, repository profile, and relevant testing skills.
---

> **Estimated context: ~0.5K tokens** (this workflow) + testing skills. Do not run tests unless explicitly allowed by user/profile.

---

## 1. Determine Scope
- **No arguments**: Test staged changes (`git diff --cached`). If empty, inform user and stop.
- **Task/description**: Identify source files via keywords/git history; read minimal context.

---

## 2. Load Testing Guidance
Load profile-specific guidance first, then target-specific generic skills:
- `view_file .agent/repository-profile.md`
- Framework-scoped testing skills (e.g. `drn-testing`, `overview-drn-testing`).
- Target skills: `test-unit`, `test-integration`, `test-integration-api`, `test-integration-db`, `test-performance` (performance only).

---

## 3. Classify
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

## 4. Plan Consolidation-First
- Extend existing test classes where possible.
- Parameterize identical bodies with test framework data attributes.
- Prefer in-process DB integration over full API/E2E if web stack is not under test.
- Keep tests separate if combining obscures different behaviors/failures.

---

## 5. Write and Verify
- Write tests per repository conventions.
- Build/test commands are permission-gated. If not explicitly allowed, do not run; report skipped.
- If allowed, run unit tests first. Discover commands from CI/files if profile is silent.

---

## 6. Self-Review
Apply Priority Stack:
- **Security**: Could fixtures, generated data, or logs leak secrets?
- **Correctness**: Does it assert meaningful behavior rather than mock plumbing?
- **Clarity**: Is the test readable without reading the source?
- **Simplicity**: Can fewer tests cover the behavior without hiding failure modes?
- **Performance**: Is runtime justified by signal?
