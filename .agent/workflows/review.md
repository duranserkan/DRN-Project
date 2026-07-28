---
description: Read-only, evidence-backed review through Priority Stack and repository criteria
---

> **Trigger**: `/review [paths | task | re-review]`
> **Estimated context: ~0.9K tokens** plus loaded review skills
> [!IMPORTANT]
> Executive Presence = structure, evidence, honesty, decisive recommendations.

## 1. Scope And Criteria

Run the Startup Gate once. Load only:

- Core: `basic-agentic-development`, `basic-code-review`, `basic-security-checklist`.
- Docs/workflows/skills: `basic-documentation`.
- Commit/branch/PR/release: `basic-git-conventions`.

| Invocation | Review scope |
|---|---|
| Paths | Complete current content; label pre-existing findings; use Git for attribution. |
| Task | Map likely files from ownership, keywords, and history. |
| None | Branch, staged, unstaged tracked, and relevant untracked scopes independently. |
| Re-review/fixes | Changed lines/behavior only; use unchanged context as proof. |

Stop when no reviewable evidence exists.

## 2. Evidence

| Scope | Inventory | Detail |
|---|---|---|
| Branch | Resolve `<merge-base>` with the algorithm below; `git diff --stat <merge-base> HEAD` | `git diff <merge-base> HEAD` |
| Staged | `git diff --cached --stat` | `git diff --cached` |
| Unstaged | `git diff --stat` | `git diff` |
| Untracked | `git ls-files --others --exclude-standard` | Read relevant files |
| Path audit | Requested paths | Read files and impact-proving references |

### Branch Base Algorithm

1. Resolve the current symbolic branch and verify that its `@{upstream}` ref resolves, then extract that upstream's remote with `git for-each-ref --format='%(upstream:remotename)' refs/heads/<current-branch>`; never infer the remote by splitting an abbreviated upstream ref. A missing or unresolvable upstream, an empty result, or a non-`.` name that does not match exactly one configured remote yields no `<current-upstream-remote>`. Treat `.` as the local-upstream marker; otherwise use only the single extracted remote and never choose among multiple remotes.
2. Use only `.agent/repository-profile.md`'s `## Conventions` `Git:` entry for configured branch selection. Resolve the current symbolic branch; choose the declared integration branch for a topic branch or the declared release branch when reviewing the integration branch. A detached `HEAD`, the release branch itself, or a missing declaration has no configured base.
3. Resolve a configured name in this order, skipping unavailable or duplicate candidates: `refs/remotes/<current-upstream-remote>/<name>` for a non-local upstream or `refs/heads/<name>` for `.`, then `refs/remotes/origin/<name>`, then `refs/heads/<name>`.
4. When no configured ref resolves, try the primary ref in this order, skipping unavailable or duplicate candidates: `refs/remotes/<current-upstream-remote>/HEAD` for a non-local upstream, `refs/remotes/origin/HEAD`, `refs/heads/main`, then `refs/heads/master`.
5. Run `git merge-base HEAD <resolved-ref>` and use the returned commit SHA as `<merge-base>` in both documented diff commands. If no candidate ref or merge base exists, report branch scope unavailable and continue with the other evidence scopes.

Report scopes separately, mark empty scopes, deduplicate findings without hiding membership, and check deleted-file references. Split diffs over 500 lines by logical group.

Do not run restore, build, apps, tests, benchmarks, or load tests. Route explicitly requested execution through its owning workflow; otherwise report `not run per repo rule`.

## 3. Evaluate

Apply Security -> Correctness -> Clarity -> Simplicity -> Performance. Use only relevant risk lenses.

Every finding must include severity, evidence, impact, violated invariant, concrete recommendation, confidence, and verification. For Critical, Major, and Suggestion fixes:

- Tag disproportionate fix complexity as `[COMPLEXITY WARNING]` metadata; complexity never lowers a Critical or Major severity.
- For Suggestions only, recommend status quo and demote to Note when the fix costs more complexity than the issue.
- Tag `[IMPROVABLE]` only when evidence shows a simpler local pattern/framework feature.

Before `/optimize`, return findings and candidates only. After it, compare the applied diff with approved scope/preview; verify metadata, references, lifecycle, and source-owned rules. CAD, `/goal`, and `/update` callers own mutations and state transitions.

## 4. Verdict

| Verdict | Rule |
|---|---|
| ✅ Approve | No unresolved Critical or Major findings and no Suggestions |
| ⚠️ Approve with Comments | Suggestions but no unresolved Critical or Major findings |
| ❌ Request Changes | Any unresolved Critical or Major finding |
| ✅ Converged | Re-review has no unresolved Critical or Major findings; remaining Suggestions accepted |

Maximum two cycles: initial plus one re-review.

State recommendations:

| Caller | Pass | Unresolved Critical/Major |
|---|---|---|
| `/update` plan (`ready`) | `transition_allowed: plan-reviewed` | `none` |
| `/update` changes (`done`) | `transition_allowed: reviewed` | `none` |
| `/optimize` | `optimization_review: passed` | `blocked` |

Report scope, verdict, findings by severity, positives/notes when useful, pre-mortem risks, and recommendations. Do not edit reviewed files.
