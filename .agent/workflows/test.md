---
description: Add tests for staged changes or a described task using DTT philosophy
---

## 1. Determine Scope

- **No extra arguments** → Test **git staged changes** (`git diff --cached`).
  - If nothing is staged, inform the user and stop.
- **Task/description provided** → Identify relevant source files by keywords, recent git history, or user guidance.
  - Read only the minimal context needed.

---

## 2. Load Testing Skills

Read to internalize DTT patterns and conventions (single source of truth — do not duplicate content here):

- `view_file .agent/skills/overview-drn-testing/SKILL.md`
- `view_file .agent/skills/test-integration/SKILL.md`
- `view_file .agent/skills/test-integration-api/SKILL.md`
- `view_file .agent/skills/test-integration-db/SKILL.md`
- `view_file .agent/skills/test-performance/SKILL.md`
- `view_file .agent/skills/test-unit/SKILL.md`

---

## 3. Analyze & Classify

- Run `git diff --cached --stat` (or read described files) to understand what changed.
- Classify each change using DTT guidance:

| Change Type | Test Type | Rationale |
|---|---|---|
| Pure business rule / utility / domain invariant | **Unit** (`DataInlineUnit`) | No external dependency needed — fast and isolated |
| Complex domain logic with multiple paths | **Unit** (`DataInlineUnit`) | Parameterized tests cover branches cheaply |
| New entity / aggregate with persistence | **Integration** (`DataInline` + Testcontainers) | Real DB validates mapping, constraints, concurrency |
| Repository / complex query | **Integration** (`DataInline` + Testcontainers) | Real SQL execution is the only honest signal |
| New API endpoint / controller | **API Integration** (`CreateClientAsync`) | Full pipeline: middleware, auth, serialization |
| Bug fix | **Regression test** (type matches the layer) | Prove the fix; prevent recurrence |
| Performance-sensitive path | **Benchmark** (only when user requests) | BenchmarkDotNet in Release mode |

> **DTT principle**: Use unit tests when integration testing is not needed. Integration tests are preferred when real dependencies provide a more honest signal, but unit tests are the right choice for pure logic, utilities, and domain invariants where containers add cost without value.

---

## 4. Plan Tests — Consolidation-First

> **Core rule**: Prefer one readable test that exercises a complete flow over many narrow tests that each assert one micro-behavior.

### Consolidation Guidelines

1. **Combine** related assertions into the same test method when they share setup and the flow remains understandable. When an integration test naturally and meaningfully covers unit-level assertions as part of a coherent flow, separate unit tests are redundant — let the integration test serve as the single source of truth. Avoid artificially grafting unrelated unit logic into an integration test solely to reduce test count.
2. **Co-locate** new tests in existing test files when a logical home exists — create a new file only when no existing file covers the component.
3. **Justify** every new test file — if an existing class already tests the same component, extend it.
4. **Prefer in-process integration** (`DrnTestContext` + Testcontainers) over full E2E (`CreateClientAsync`) when the web stack isn't under test — faster execution, same correctness.
5. **Parameterize** instead of duplicating — use `[DataInline(...)]` / `[DataInlineUnit(...)]` with multiple attribute rows to cover branches in a single test method.

### Runtime Performance Awareness

- Unit tests: milliseconds — use freely.
- DB integration tests: seconds (container startup is shared) — combine related assertions to reduce per-test overhead.
- API integration tests: most expensive — reserve for endpoint-level behavior, combine related endpoint calls in one test when the flow is sequential and readable.

---

## 5. Write & Run

- Write tests following the patterns from the loaded skills (DTT, `DrnTestContext`, `DataInline` attributes).
- **Run and verify** (verify project names match the current solution structure):
  - Unit: `dotnet test DRN.Test.Unit`
  - Integration: `dotnet test DRN.Test.Integration`
- **If container startup fails**: verify Docker is running and retry. If tests fail, diagnose before writing more tests — avoid compounding failures.
- Report results to the user: pass/fail, execution time, any failures with diagnostics.

---

## 6. Self-Review (Quality Gate)

Apply Priority Stack to the written tests before presenting them:

| Gate | Question |
|---|---|
| **Security** | Could test data, fixtures, or configuration leak secrets or weaken security posture? |
| **Correctness** | Does each test assert meaningful behavior, not mock plumbing? |
| **Clarity** | Can someone understand the test in 6 months without reading the source? |
| **Simplicity** | Could fewer tests cover the same surface? Is any test redundant? |
| **Performance** | Is the total added execution time justified by the coverage gained? |

If any gate fails, refactor the tests before reporting done.
