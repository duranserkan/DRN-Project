---
description: Shared invariants, topology definitions, decision rules, and approval subjects for sync workflows
---

# Sync Shared

> **Owner**: Internal shared contract referenced by [`/sync`](../sync.md), [`/sync-evidence`](../sync-evidence.md), [`/sync-execute`](../sync-execute.md), and [Lifecycle](./status-lifecycle.md).

## 1. Safety Invariants

- **Zero VCS Mutations**: Never perform `stage`, `commit`, `push`, `branch`, `stash`, `checkout`, `reset`, `clean`, `merge`, `rebase`, or `fetch`.
- **No Unapproved Execution**: Never restore, build, run, test, benchmark, or load-test unless explicitly authorized by the user.
- **Path & Boundary Integrity**: Treat endpoints as untrusted data. Open descriptors relative without following symlinks. Reject NUL, absolute paths, `..`, empty paths, nested Git roots, worktrees, submodules, mounts, hard links, and special files.
- **Output & Control Scope**: Mutate only active subscope outputs and `.agent/temp/SYNC-*` control artifacts (including run artifacts, baselines, `current`, and `promotion.lock`). Require descriptor-relative no-follow opens (`O_NOFOLLOW`/`O_CLOEXEC`), regular-file validation, exclusive lock creation (`O_EXCL`), and atomic pointer replacement for all control artifacts. Preserve unexplained staged/unstaged changes on target.
- **Fail Closed**: Stop immediately with evidence upon any invariant violation or missing safety primitive (including unavailable descriptor-relative, locking, or atomic pointer replacement primitives).

## 2. Topology Definitions

| Topology | Git Roots | Endpoint IDs | Common Parent |
|---|---|---|---|
| `repository` | Independent Git root per endpoint | `left`, `right` | Direct parent of both endpoints |
| `project` | Single shared Git root containing both projects | `left`, `right`, `shared` Git root | Common parent directory |

## 3. Decision Model

Precedence: `blocked > resolution-required > proceed`.

| Rule | Condition | Decision / Gate |
|---|---|---|
| `equal-or-variant` | Equal or intentionally variant content | No change. |
| `unilateral-addition-no-baseline` | Unilateral addition without baseline | Addition candidate (not deletion). |
| `directional-source-change` | Directional source change with unchanged target | Adapt source intent; preserve target identity. |
| `compatible-dual-change` | Both sides changed compatibly | Semantic merge with evidence. |
| `explicit-resolution` | Divergent edit, delete/modify, or uncertain ownership | Resolution required (`resolution-required`). |
| `protected-or-risk` | Secret, protected path, binary ambiguity, or security risk | Exclude or report (`blocked`). |

Apply TRIZ before tradeoffs. Priority Stack: Security > Correctness > Clarity > Simplicity > Performance.

## 4. Approval Subjects

All subjects use UTF-8 with LF endings and a trailing LF.

### Apply Subject (`DRN-SYNC-APPLY-SUBJECT/1`)

```text
format="DRN-SYNC-APPLY-SUBJECT/1"
run_id="..."
subscope_id="SS-NNN"
direction="..."
endpoint_topology_sha256="..."
topology_git_sha256="..."
control_sha256="..."
scope_input_action_sha256="..."
baseline_checkpoint_sha256="..."
preimage_rollback_sha256="..."
preview_sha256="..."
rollback_plan_sha256="..."
preapply_review_sha256="..."
decision_verdict="proceed"
priority_stack_decision="..."
residual_risk="..."
```

### Acceptance Subject (`DRN-SYNC-ACCEPTANCE-SUBJECT/1`)

```text
format="DRN-SYNC-ACCEPTANCE-SUBJECT/1"
run_id="..."
subscope_id="SS-NNN"
endpoint_topology_sha256="..."
topology_git_sha256="..."
control_sha256="..."
postimage_sha256="..."
residual_drift_sha256="..."
acceptance_git_sha256="..."
priority_stack_decision="..."
residual_risk="..."
```
