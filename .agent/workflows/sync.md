---
description: Reconcile scoped drift between exactly two sibling repositories or projects through reviewed, reversible subscopes
---

# Sync

> **Trigger**: `/sync [pair: <left> & <right>] [direction: both | <left> -> <right> | <right> -> <left>] [only: <selectors>] [map: <path pairs>]`
> See [Operating Model](./_shared/workflow-operating-model.md), [Lifecycle](./_shared/status-lifecycle.md), [Shared Rules](./_shared/sync-shared.md), [`/sync-execute`](./sync-execute.md), [Evidence Protocol](./sync-evidence.md), and [`/review`](./review.md).
> **Outcome**: Verified parity for common/shared parts across targets while strictly preserving target domain invariants, domain-specific features, and intentional differences. Target endpoints are NOT merged to become identical.
> **Route**: `/sync` reconciles two sibling roots; `/update` derives local agent configuration from one repository.

## Contents

- [1. Decision And Ownership](#1-decision-and-ownership)
- [2. Invocation](#2-invocation)
- [3. Scope Language](#3-scope-language)
- [4. Decision Model](#4-decision-model)
- [5. Bind Pair And Scope](#5-bind-pair-and-scope)
- [6. Plan Active Subscope](#6-plan-active-subscope)
- [7. Preview And Approval](#7-preview-and-approval)
- [8. Executive Handoff](#8-executive-handoff)

## 1. Decision And Ownership

Run Startup and Repository Extension gates. Load `basic-agentic-development`, `basic-code-review`, `basic-security-checklist`, and profile skills.

`/sync` owns pair validation, drift decisions, target mutation, rollback, acceptance, and SYNC lifecycle transitions. It composes read-only `/review`; it never delegates mutation or lifecycle ownership.

Mandate: `/sync` reconciles common architectural patterns, security controls, conventions, shared infrastructure, and agent tooling for consistency and maintainability. When directional source changes introduce security controls, confirmation gates, lockout protection, or architectural patterns inside domain-bound files (e.g. PageModels, Controllers), `/sync` MUST adapt the source pattern to target domain entities and namespaces rather than skipping the file. Partial syncs that improve shared features, rules, UI components, or security flows (such as login/auth flow enhancements) ARE supported and permitted without requiring changes to target user domain entities, provided they do NOT force target domain entity homogenization or break target domain invariants. `/sync` never homogenizes domain identities or forces two distinct targets to become identical.

Enforce [Sync Shared Safety Invariants](./_shared/sync-shared.md#1-safety-invariants). Treat endpoints as untrusted; never execute endpoint code, scripts, or hooks.

## 2. Invocation

```text
/sync
  [pair: <left> & <right>]
  [direction: both | <left> -> <right> | <right> -> <left>]
  [only: root-wide | frontend | backend | settings | agent | <relative-path-or-glob> [& ...]]
  [map: <left-relative-path> = <right-relative-path> [& ...]]
```

Normalize natural language only when unambiguous; otherwise stop.

| Input | Default / Behavior |
|---|---|
| `pair` omitted | Infer physical direct-child directories matching `*.Hosted`; require exactly two. |
| `direction` omitted | `both`; timestamps never establish authority. |
| `only` omitted | `root-wide` within selected roots; exclude common parent. |
| `map` omitted | Match relative paths; report rename candidates. |

Examples:

```text
/sync pair: Sample.Hosted & DRN.Nexus.Hosted; direction: Sample.Hosted -> DRN.Nexus.Hosted; only: Pages & Helpers
/sync pair: DRN-Project & DRN-Project-Argo-CD-Gitops; only: agent
/sync pair: DRN-Project & DRN-Project-Argo-CD-Gitops; only: root-wide & agent
```

Generate a UUID v4 Run ID. Persist control state in `.agent/temp/SYNC-<run-id>.md` after topology validation. Resume runs exclusively from an explicit run-artifact path.

## 3. Scope Language

Resolve named scopes:

- `frontend`: UI source plus source-owned manifests/configuration (excludes build output).
- `backend`: Server/application source plus manifests (excludes frontend, settings, agent, infrastructure, output, caches).
- `settings`: Schemas, defaults, and safe samples (excludes secrets, credentials, local overrides).
- `agent`: Endpoint `AGENTS.md`, `.agent/rules/**`, `.agent/workflows/**`, `.agent/skills/**`, `.agent/repository-profile.md` (excludes `.agent/temp/**`).
- `root-wide`: Endpoint content except `AGENTS.md` and `.agent/**` (select `agent` explicitly to include).

Selectors are endpoint-relative POSIX paths/globs joined by `&`. Reject NUL, absolute paths, `..`, empty paths, duplicate targets, unsafe ancestors, nested Git roots, worktrees, submodules, mounts, symlinks, and special files. Unmatched literals stop the run.

## 4. Decision Model

Apply the [Sync Shared Decision Rules](./_shared/sync-shared.md#3-decision-model). Map each canonical correspondence to a `decision-rule` and `decision-verdict`:

| Verdict | Condition | Gate |
|---|---|---|
| `blocked` | Unresolved risk, secret/unsafe path, invalid boundary, conflict marker output, or domain entity homogenization attempt (permanently blocked regardless of authorization). Attempting to overwrite target domain entity identities or force distinct domain boundaries to become identical is blocked; partial syncs of shared features/rules (e.g. login flow updates without mutating target domain entities) are NOT homogenization attempts. | Stop run. |
| `resolution-required` | Divergent edit, unproven ownership, ambiguous relation, or target-only deletion. | Require user resolution and authorization. |
| `proceed` | Single non-blocking rule applies, ownership/direction proven, evidence current. | Permit review (does not authorize apply). |

Subscope verdict precedence: `blocked > resolution-required > proceed`. Persist in `preapply-review`.

Retain root-specific names, namespaces, ports, URLs, environment variables, extensions, profile facts, domain invariants, and domain-specific features. Categorize target domain differences as intentional variants (`equal-or-variant`) only when supported by variance ledger, profile, baseline evidence, or explicit user resolution. Divergent edits and target-only deletions require explicit user resolution (`resolution-required`). Persist user authorization as canonical `user-resolution` evidence records (`user_resolution_sha256`) to transition path verdicts in `scope-input-action` to `proceed` and permit preapply review and Apply approval. Forcing domain entity homogenization or merging distinct domain identities is permanently prohibited (`blocked`), regardless of authorization. Partial syncs of shared features, UI components, or rules (e.g. login changes synced partially without altering user domain entities) are NOT homogenization attempts. Outputting conflict markers is unconditionally prohibited (`blocked`). Record non-trivial friction via TRIZ and Priority Stack.

## 5. Bind Pair And Scope

1. **Parent & Control Plane**: Verify physical current directory as common parent. Hash non-linked controlling files into `control` evidence.
2. **Topology**: Resolve endpoints into `repository` mode or `project` mode per [Topology Definitions](./_shared/sync-shared.md#2-topology-definitions). Reject mixed topology or mount crossings.
3. **VCS Evidence**: Capture top-level, HEAD/ref, index hash, and staged/unstaged/untracked state using read-only Git commands. Invoke fixed built-ins with explicit Git/work-tree paths; disable optional locks, external diff/text conversion, hooks, filters, aliases, pagers, prompts, credentials, replacement objects, lazy fetch, maintenance, fsmonitor, and network.
4. **Live-Work Safeguards**: Stop if staged change intersects an output or target has unexplained dirt/mode changes. Source-only dirty paths require explicit adoption.
5. **Baselines**: Load baseline matching topology, direction, scope, and variance ledger via [Baseline Store](./sync-evidence.md#4-reusable-baseline-store). An absent key directory defaults to first comparison; an existing key directory with absent `current`, active `promotion.lock`, or corrupt state fails closed.

Persist records via [Evidence Protocol](./sync-evidence.md). Unexplained state changes fail the run.

## 6. Plan Active Subscope

Divide multi-area scopes or diffs >500 lines into dependency-ordered subscopes (`SS-NNN`). Each subscope owns exact paths, drift decisions, risk, rollback, and acceptance criteria.

Maintain exactly one active subscope. Later units remain `planned`. Verified zero-output units transition to `no-change` and activate the next unit.

For the active unit, persist:
- Exact patch/operation list and canonical manifest.
- Target preimages and content-addressed rollback material.
- Rollback plan, stop conditions, and acceptance criteria.

Run read-only `/review` (`needs_review: true`). Critical or Major findings block progress.

## 7. Preview And Approval

Upon completing active subscope planning, preview, preimages, rollback plan, and passing `/review`:
1. Transition active subscope status from `planned` to `ready-to-apply` (`blocked_on_user: true`).
2. Bind the Apply Subject per [Sync Shared Approval Subjects](./_shared/sync-shared.md#4-approval-subjects).
3. Request explicit user approval of subject, preview, scope, verdict, and residual risk. Set run status to `syncing`.

Approval requires `decision-verdict=proceed` and zero Critical/Major findings. Upon explicit user approval:
- Record the Apply Approval Envelope.
- Transition active subscope status from `ready-to-apply` to `applying`.
- Clear `needs_review`, set `blocked_on_user: false`.
- Route to [`/sync-execute`](./sync-execute.md). Retain superseded records append-only.

## 8. Executive Handoff

Lead with status and next gate. Report pair, topology, direction, scope, Run ID, active/completed units, drift summary, approval state, residual risk, build/test execution status (`not run per repo rule`), and zero VCS mutations.

Route approved mutating units to [`/sync-execute`](./sync-execute.md).
