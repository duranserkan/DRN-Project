---
description: Shared invariants, topology definitions, decision rules, and approval subjects for sync workflows
---

# Sync Shared

> **Owner**: Internal shared contract referenced by [`/sync`](../sync.md), [`/sync-evidence`](../sync-evidence.md), [`/sync-execute`](../sync-execute.md), and [Lifecycle](./status-lifecycle.md).

## 1. Safety Invariants

- **Zero VCS Mutations**: Never perform `stage`, `commit`, `push`, `branch`, `stash`, `checkout`, `reset`, `clean`, `merge`, `rebase`, or `fetch`.
- **No Unapproved Execution**: Never restore, build, run, test, benchmark, or load-test unless explicitly authorized by the user.
- **Standard Tools Only**: Use built-in read/edit capabilities and standard commands (`git`, `find`, `stat`, `shasum`, `cmp`, `mkdir`, `mv`). Never create or execute ad-hoc serializers, scripts, binaries, or endpoint code. Fail closed when standard capabilities cannot prove a required condition.
- **Endpoint Model**: Treat endpoint content as untrusted. Require explicit user ownership and quiescence from final pre-mutation revalidation through postimage or rollback verification. Recorded dirt is not concurrent mutation. Adversarial races are unsupported. Observed drift stops mutation.
- **Path & Boundary Integrity**: Use the [evidence path bases](../sync-evidence.md#1-evidence-rules). Require UTF-8, control-character-free relative paths. Reject empty or absolute paths, `..`, unsafe ancestors, symlinks, nested Git roots, worktrees, submodules, mount crossings, hard links, and special files using non-following standard metadata checks.
- **Output & Control Scope**: Mutate only approved active-subscope outputs and `.agent/temp/SYNC-*` controls. Immediately before every apply, rollback, or recovery write, revalidate exact approved/current presence, identity, mode, size, and content hashes. Verify the exact postimage afterward. Persist and verify control state before output mutation and preserve unexplained work.
- **Fail Closed**: Stop with evidence on unsupported objects, failed commands, unavailable required checks, or any observed state change.

## 2. Topology Definitions

| Topology | Git Roots | Endpoint IDs | Common Parent |
|---|---|---|---|
| `repository` | Independent Git root per endpoint | `left`, `right` | Direct parent of both endpoints |
| `project` | Single shared Git root containing both projects | `left`, `right`, `shared` Git root | Common parent directory |

## 3. Decision Model

Precedence: `blocked > resolution-required > proceed`.

| Rule | Condition | Decision / Gate |
|---|---|---|
| `equal-or-variant` | Equal or documented/evidence-proven intentional variant | Preserve target identity and domain invariants. No change. |
| `unilateral-addition-no-baseline` | Unilateral addition without baseline | Addition candidate (not deletion). |
| `directional-source-change` | Source changed, target unchanged, and current evidence proves the changed behavior is shared and does not conflict with an intentional target security or domain variant | Adapt source intent under the criteria below. Preserve target identity and domain invariants. |
| `compatible-dual-change` | Both sides changed compatibly | Semantic merge with evidence. |
| `explicit-resolution` | Divergent edit, target-only deletion, delete/modify, or uncertain ownership | Resolution required (`resolution-required`). Recorded user resolution via `user-resolution` evidence transitions verdict to `proceed`. |
| `protected-or-risk` | Secret, protected path, binary ambiguity, conflict marker output, domain entity homogenization attempt (attempting to overwrite target domain entity identities or merge distinct domain identities), or security risk | Exclude or report (`blocked`). |

Never merge distinct domain identities, homogenize target domain entities, or erase target-specific invariants, regardless of authorization. Evidence-backed intentional security or domain variants remain `equal-or-variant`.

Partial syncs of shared features, UI, rules, or authentication flows are supported within selected scope without changing target domain entities. For example, adapt a shared login pattern in a PageModel or Controller by substituting target user types and namespaces. First prove shared behavior and no conflict with intentional variants. Preserve security controls, confirmation gates, lockout protection, defensive validation, and domain boundaries. Such adaptations are not homogenization and must not force endpoints to become identical.

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
user_resolution_sha256="..."
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

### Recovery Subject (`DRN-SYNC-RECOVERY-SUBJECT/1`)

```text
format="DRN-SYNC-RECOVERY-SUBJECT/1"
run_id="..."
subscope_id="SS-NNN"
endpoint_topology_sha256="..."
control_sha256="..."
apply_progress_sha256="..."
partial_postimage_sha256="..."
recovery_plan_sha256="..."
recovery_decision="retry-rollback|terminate-partial"
priority_stack_decision="..."
residual_risk="..."
```
