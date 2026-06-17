---
description: Review files, staged changes, branch diffs, or task-scoped changes using Priority Stack and project review skills
---

> **Trigger**: `/review [paths | task description | re-review]`
> **Mission**: produce read-only, evidence-backed findings and a verdict.
> **Estimated context: ~0.7K tokens** + loaded review skills
> See also: [Operating Model](./_shared/workflow-operating-model.md), [`/optimize`](./optimize.md)
>
> [!IMPORTANT]
> **Executive Presence governs every stage**: structured evaluation, evidence-based findings, honest verdicts, decisive recommendations.

---

## 1. Resolve Scope
Run the shared Startup Gate once, reuse loaded context, and load only review skills needed for the scoped material.

| Invocation | Scope Rule |
|---|---|
| File path(s) | Read specified files directly; skip git analysis unless needed for changed-line context. |
| Task description | Identify likely files by keywords, ownership, and recent git history; read minimal context. |
| No arguments | Determine integration/release branch from profile or primary refs; review branch diff, then staged diff if branch diff is empty. |
| Re-review/check fixes | Evaluate changed lines or changed behavior only; inspect unchanged context only to prove impact. |

If no diff, file, or task evidence exists, report that there is nothing reviewable and stop.

---

## 2. Load Criteria
Use skills as source of truth; do not duplicate their checklists.

| Load | Skills |
|---|---|
| Core | `basic-agentic-development`, `basic-code-review`, `basic-security-checklist` |
| Docs/workflows/skills | `basic-documentation` |
| Branch, commit, PR, release, or VCS policy | `basic-git-conventions` |

---

## 3. Analyze
| Case | Action |
|---|---|
| Branch/staged diff | Run `git diff --stat`, then full diff. Confirm deleted files leave no dangling references. |
| Paths/task | Read scoped files plus only the references needed to prove or disprove impact. |
| Large diff >500 lines | Split into logical groups, review each group, then synthesize one verdict. |
| Before `/optimize` | Return findings and optimization candidates only; do not edit or approve apply. |
| After `/optimize` | Review the optimized diff against previewed scope, candidate set, and severity; verify frontmatter, references, lifecycle metadata, and source-owned rules. |
| Called by CAD or `/goal` | Return the report template; caller owns artifact state, mutation, and completion. |

---

## 4. Evaluate
1. Apply Priority Stack in order: Security -> Correctness -> Clarity -> Simplicity -> Performance. A failed higher gate blocks lower gates.
2. Apply loaded skill criteria and the shared Evidence Contract to every finding.
3. For 🔴 Critical or 🟡 Suggestion findings, run the recommendation self-check:
   - If the fix is more complex than the finding, tag `[COMPLEXITY WARNING]`, recommend status quo, and demote to 🔵 Note unless severity is 🔴.
   - Compare current approach with a simpler local pattern or framework feature. Tag `[IMPROVABLE]` only when a better alternative is evidenced.
4. Run risk lenses only where relevant: pre-mortem, second-order effects, Five Whys for bug fixes, systems boundaries, and malicious/null/large/concurrent inputs.

---

## 5. Produce Review Report
Produce a report; do not edit reviewed files. Omit empty sections except when no findings exist, in which case state that clearly.

| Verdict | Condition |
|---|---|
| ✅ Approve | No 🔴 Critical findings |
| ⚠️ Approve with Comments | No 🔴 Critical, but 🟡 Suggestions present |
| ❌ Request Changes | Any 🔴 Critical finding |
| ✅ Converged | Re-review: no new 🔴 Critical (remaining 🟡 accepted) |

> **Iteration limit**: Max 2 cycles (initial + 1 re-review). Remaining 🟡 are accepted after re-review.

### State Hooks
`/review` is read-only. Report state recommendations; callers perform mutations.

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
- [file:line · evidence · impact · violated invariant · recommendation · confidence · verification · `[IMPROVABLE]` if better alternative exists]

### 🟡 Suggestions
- [file:line · evidence · impact · violated invariant · recommendation · confidence · verification · `[IMPROVABLE]` if better alternative exists]

### 🟢 Positive
- [pattern observed]

### 🔵 Notes
- [informational observation · no action required]

## Pre-Mortem Risks
- [risk or "None"]

## Recommendations
- [next steps]
```
