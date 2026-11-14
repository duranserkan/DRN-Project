---
description: Read-only, evidence-backed review through Priority Stack and repository criteria
---

> **Trigger**: `/review [paths | task | re-review]`

## 1. Scope And Criteria

Run Startup Gate once. Load:

- Core: `basic-agentic-development`, `basic-code-review`, `basic-security-checklist`.
- Docs/workflows/skills: `basic-documentation`.
- Commit/branch/PR/release: `basic-git-conventions`.

| Invocation | Review scope |
|---|---|
| Paths | Complete current content. Label pre-existing findings and use Git for attribution. |
| Task | Map likely files from ownership, keywords, and history. |
| None | Branch, staged, unstaged tracked, and relevant untracked scopes independently. |
| Re-review/fixes | Changed lines/behavior only. Use unchanged context as proof. |

Stop without reviewable evidence.

## 2. Evidence

| Scope | Inventory | Detail |
|---|---|---|
| Branch | Resolve `<merge-base>` below, then `git diff --stat <merge-base> HEAD` | `git diff <merge-base> HEAD` |
| Staged | `git diff --cached --stat` | `git diff --cached` |
| Unstaged | `git diff --stat` | `git diff` |
| Untracked | `git ls-files --others --exclude-standard` | Read relevant files |
| Path audit | Requested paths | Read files and impact-proving references |

### Branch Base Algorithm

1. Resolve the current symbolic branch and its `@{upstream}` ref. Extract the upstream remote with `git for-each-ref --format='%(upstream:remotename)' refs/heads/<current-branch>`. Never split an abbreviated upstream ref. Missing/unresolvable upstream, empty output, or a non-`.` name without exactly one configured remote yields no `<current-upstream-remote>`. `.` means local upstream. Otherwise use the single extracted remote without choosing among remotes.
2. Use only the profile's `## Conventions` `Git:` entry for configured branch selection. Choose the integration branch for a topic branch, or the release branch when reviewing integration. Detached `HEAD`, the release branch itself, or a missing declaration has no configured base.
3. Resolve a configured name in this order, skipping unavailable or duplicate candidates: `refs/remotes/<current-upstream-remote>/<name>` for a non-local upstream or `refs/heads/<name>` for `.`, then `refs/remotes/origin/<name>`, then `refs/heads/<name>`.
4. When no configured ref resolves, try the primary ref in this order, skipping unavailable or duplicate candidates: `refs/remotes/<current-upstream-remote>/HEAD` for a non-local upstream, `refs/remotes/origin/HEAD`, `refs/heads/main`, then `refs/heads/master`.
5. Run `git merge-base HEAD <resolved-ref>`. Use its SHA as `<merge-base>` in both diff commands. Without a candidate ref or merge base, report branch scope unavailable and review other scopes.

Report scopes separately, mark empty scopes, deduplicate findings without hiding membership, and check deleted-file references. Split diffs over 500 lines by logical group.

Never restore, build, run apps/tests/benchmarks/load tests. Route explicitly requested execution through its owner. Otherwise report `not run per repo rule`.

## 3. Evaluate

Resolve contradictions with TRIZ, then apply Security -> Correctness -> Clarity -> Simplicity -> Performance with relevant lenses.

Check rendering conditions and refactors against [basic-code-review](../skills/basic-code-review/SKILL.md#architecture-checks).

For changed CLI inputs, apply the [path and child-process checks](../skills/basic-security-checklist/SKILL.md#cli-paths-and-child-process-arguments). For input-controlled redirects, apply the [destination checks](../skills/basic-security-checklist/SKILL.md#redirect-destinations). Review test setup against [fixture constraints](../skills/basic-code-review/SKILL.md#test-coverage-expectations) before accepting coverage claims.

Every finding must include severity, evidence, impact, violated invariant, concrete recommendation, confidence, and verification. For Critical, Major, and Suggestion fixes:

- Tag disproportionate fix complexity as `[COMPLEXITY WARNING]`. Complexity never lowers Critical or Major severity.
- For Suggestions only, recommend status quo and demote to Note when the fix costs more complexity than the issue.
- Tag `[IMPROVABLE]` only when evidence shows a simpler local pattern/framework feature.

Before `/optimize`, return only findings/candidates. Afterward, check the diff against approved scope/preview, metadata, references, lifecycle, and source-owned rules. CAD, `/goal`, and `/update` callers own mutations/transitions.

## 4. Verdict

| Verdict | Rule |
|---|---|
| ✅ Approve | No unresolved Critical or Major findings and no Suggestions |
| ⚠️ Approve with Comments | Suggestions but no unresolved Critical or Major findings |
| ❌ Request Changes | Any unresolved Critical or Major finding |
| ✅ Converged | Re-review has no unresolved Critical or Major findings. Remaining Suggestions accepted |

Default to two cycles: initial plus one re-review. Further corrective iterations require explicit user authorization. Never mark unresolved Critical or Major findings as passed because a cycle limit was reached.

State recommendations:

| Caller | Pass | Unresolved Critical/Major |
|---|---|---|
| `/update` plan (`ready`) | `transition_allowed: plan-reviewed` | `none` |
| `/update` changes (`done`) | `transition_allowed: reviewed` | `none` |
| `/optimize` | `optimization_review: passed` | `blocked` |

Report scope, verdict, severity-ranked findings, useful notes, pre-mortem risks, and recommendations. Never edit reviewed files.
