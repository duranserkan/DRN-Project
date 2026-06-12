---
description: Shared operating model for agent workflows
---

> Load with task workflows that mutate or review agent-consumed documents.

## Startup Gate

1. Read `AGENTS.md`.
2. Read `.agent/rules/DiSCOS.md` when present.
3. Read `.agent/repository-profile.md` when present.
4. Read the target workflow and only the skills needed for the task.

Reuse context already loaded in the same session. Do not re-read broad skill sets when a focused skill or workflow is enough.

## Priority Queue

Classify work before acting:

| Priority | Meaning | Action |
|---|---|---|
| `P0` | Security, correctness, data loss, irreversible mutation, broken lifecycle | Fix first; block handoff until resolved |
| `P1` | Ambiguity, portability drift, missing evidence, maintainability risk | Fix in the current pass unless explicitly scoped out |
| `P2` | Polish, deduplication, token efficiency, minor references | Fix after P0/P1 when low risk |

When priorities conflict, use TRIZ first: seek an option that satisfies both constraints. If a real tradeoff remains, apply the Priority Stack: Security, Correctness, Clarity, Simplicity, Performance.

## Mutation Tiers

| Tier | Examples | Rule |
|---|---|---|
| Read-only | Research, review, search, preview, dry run | Never edit files or state |
| Reversible docs | Workflow, skill, README, report edits | Edit only scoped files; run `git diff --check` |
| State mutation | Status flags, temp plan progress, generated handoff docs | Allowed only when the owning workflow says so |
| VCS mutation | Branch creation, commits, rewrites, tags, push | Requires explicit user approval unless the user directly requested that exact action |
| Destructive | Delete, reset, force push, data migration | Requires explicit approval and a rollback plan |

Do not rely on "git diff is rollback" as approval for unrequested edits. Preview-first workflows must not mutate until the apply step is confirmed.

## Portable Tool Verbs

Use capability names in reusable workflows. Map them to the active platform's tools at execution time.

| Need | Portable wording |
|---|---|
| Read a known file | Read file |
| Search text or identifiers | Search text |
| Find paths by name or glob | Find files |
| Inspect code structure | Inspect outline/symbols |
| Read agent knowledge resources | List/read knowledge resources |
| Search external sources | Web search or read URL |

Prefer repository facts and source files over external references. Use web sources only when internal sources are insufficient or the user requests current outside information.

## Evidence Contract

Every review finding or workflow gate failure must include:

- **Evidence**: file/path and line, command output, or source link.
- **Impact**: what breaks, leaks, confuses, or slows.
- **Invariant**: which rule, status contract, or priority gate is violated.
- **Recommendation**: concrete next action.
- **Confidence**: high, medium, low.
- **Verification**: run, not run per rule, blocked, or not applicable.

Findings are caused by changed lines or changed behavior, but reviewers may inspect unchanged context needed to prove or disprove impact.

## Verification

After workflow, skill, or documentation edits:

1. Check scoped diffs against the requested plan.
2. Run `git diff --check` unless blocked.
3. Run any additional static checks listed by the active workflow.
4. Report build/test commands as "not run per repo rule" when the repository profile or user did not allow them.
