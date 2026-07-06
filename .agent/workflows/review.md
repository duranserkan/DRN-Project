---
description: Review scoped files or diffs through Priority Stack and project review skills
---

> **Trigger**: `/review [paths | task description | re-review]`
> **Mission**: Return read-only, evidence-backed findings and verdict.
> **Estimated context: ~1.1K tokens** + loaded review skills
> See also: [Operating Model](./_shared/workflow-operating-model.md), [`/optimize`](./optimize.md)
>
> [!IMPORTANT]
> Executive Presence = structure, evidence, honesty, decisive recommendations.

## 1. Scope
Run the shared Startup Gate once. Reuse context. Load only needed review skills.

| Invocation | Scope |
|---|---|
| File path(s) | Read paths directly; use git only for changed-line context. |
| Task description | Map likely files by keywords, ownership, and recent history; read minimal context. |
| No arguments | Determine integration/release branch from profile or primary refs; review branch diff, then staged diff if empty. |
| Re-review/check fixes | Evaluate changed lines or changed behavior only; inspect unchanged context only to prove impact. |

If no evidence exists, report nothing reviewable and stop.

## 2. Criteria
Use skills as source of truth. Do not duplicate checklists.

| Load | Skills |
|---|---|
| Core | `basic-agentic-development`, `basic-code-review`, `basic-security-checklist` |
| Docs/workflows/skills | `basic-documentation` |
| Branch, commit, PR, release, or VCS policy | `basic-git-conventions` |

## 3. Analyze
| Case | Action |
|---|---|
| Branch/staged diff | Run `git diff --stat`, then full diff. Check deleted files leave no dangling references. |
| Paths/task | Read scoped files plus references needed to prove or disprove impact. |
| Large diff >500 lines | Split by logical group. Review each group. Synthesize one verdict. |
| Before `/optimize` | Return findings and optimization candidates only; do not edit or approve apply. |
| After `/optimize` | Compare optimized diff with previewed scope, candidates, and severity. Verify frontmatter, references, lifecycle metadata, and source-owned rules. |
| CAD or `/goal` caller | Return the report template; caller owns artifact state, mutation, and completion. |

Do not run restore, build, app, test, benchmark, or load-test commands during `/review`. If the user explicitly requests execution, route that work through the appropriate workflow under the shared Command Execution Authorization Gate. Otherwise inspect recorded verification and report missing execution as `not run per repo rule`.

## 4. Evaluate
1. Apply Priority Stack: Security -> Correctness -> Clarity -> Simplicity -> Performance. Higher failure blocks lower gates.
2. Apply loaded skill criteria and the shared Evidence Contract to every finding.
3. For 🔴 Critical or 🟡 Suggestion recommendations:
   - Tag `[COMPLEXITY WARNING]`, recommend status quo, and demote to 🔵 Note when the fix is more complex than the finding and severity is not 🔴.
   - Tag `[IMPROVABLE]` only when evidence shows a simpler local pattern or framework feature.
4. Use relevant risk lenses only: pre-mortem, second-order effects, Five Whys for bug fixes, systems boundaries, malicious/null/large/concurrent inputs.

## 5. Report
Return a report. Do not edit reviewed files. Omit empty sections; if no findings exist, state that clearly.

| Verdict | Condition |
|---|---|
| ✅ Approve | No 🔴 Critical findings |
| ⚠️ Approve with Comments | No 🔴 Critical, but 🟡 Suggestions present |
| ❌ Request Changes | Any 🔴 Critical finding |
| ✅ Converged | Re-review: no new 🔴 Critical (remaining 🟡 accepted) |

> **Iteration limit**: Max 2 cycles (initial + 1 re-review). Remaining 🟡 are accepted after re-review.

### State Hooks
Read-only: report state recommendations; callers mutate.

| Caller | No 🔴 Critical | 🔴 Critical Present |
|---|---|---|
| `/update` plan review from `Status: ready` | `transition_allowed: plan-reviewed` | `transition_allowed: none` |
| `/update` changes review from `Status: done` | `transition_allowed: reviewed` | `transition_allowed: none` |
| `/optimize` quality gate | `optimization_review: passed` | `optimization_review: blocked` |

### Report Template
```markdown
## Review Summary
**Scope**: [branch changes | staged changes | task description]
**Verdict**: ✅ / ⚠️ / ❌

## Findings
### 🔴 Critical
- [file:line · evidence · impact · invariant · recommendation · confidence · verification · `[IMPROVABLE]` when evidenced]

### 🟡 Suggestions
- [file:line · evidence · impact · invariant · recommendation · confidence · verification · `[IMPROVABLE]` when evidenced]

### 🟢 Positive
- [pattern observed]

### 🔵 Notes
- [informational observation · no action required]

## Pre-Mortem Risks
- [risk or "None"]

## Recommendations
- [next steps]
```
