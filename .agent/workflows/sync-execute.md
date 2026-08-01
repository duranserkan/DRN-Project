---
description: Apply one approved sync subscope, verify its postimage, and obtain acceptance
---

# Sync Execute

> **Trigger**: Resume an explicit [`/sync`](./sync.md) Run artifact, or perform final verification when all units are terminal.
> **Exit**: Next unit activated, or run `verified`/`failed`.
> See [Operating Model](./_shared/workflow-operating-model.md), [Lifecycle](./_shared/status-lifecycle.md), [Shared Rules](./_shared/sync-shared.md), [Evidence Protocol](./sync-evidence.md), and [`/review`](./review.md).

## 1. Dispatch Current State

Inspect run status and active-subscope marker:

| Current State | Action / Route |
|---|---|
| Active `planned` | Return to `/sync` planning, review, and preview. |
| Active `ready-to-apply` | Continue to [Revalidate Approval](#2-revalidate-approval) (sole entry to apply upon explicit user approval). |
| Active `applying` | Treat write set as indeterminate; proceed to [Failure And Rollback](#4-failure-and-rollback). |
| Active `rolling-back` | Resume rollback proof in [Failure And Rollback](#4-failure-and-rollback). |
| Active `applied` | Continue to [Verify Postimage](#5-verify-postimage). |
| Active `awaiting-user-approval` | Resume pending acceptance in [Post-Change Acceptance](#6-post-change-acceptance). |
| Active `awaiting-user-commit` | Continue to [Verify User Commit](#7-verify-user-commit). |
| Active `partially-committed` | Continue to [Verify User Commit](#7-verify-user-commit) for pending roots. |
| All units `committed` / `no-change`, run `syncing` | Proceed to full-scope verification in [Advance And Verify Run](#8-advance-and-verify-run). |
| Run `verified` / `failed` | Revalidate/report terminal state. |

Invalid state combinations fail closed.

## 2. Revalidate Approval

1. Recompute approval envelope, apply-subject hash, preview hash, and evidence hashes.
2. Revalidate roots, topology, control plane, Git state, ancestors, scope, subscope `SS-NNN`, inputs, and preimages.
3. Confirm approval matches target paths, operations, outputs, and residual risk.
4. Verify zero unresolved Critical/Major findings in `/review`.

If revalidation detects recoverable input or preview drift (where topology, control plane, and scope boundaries remain intact), invalidate approval append-only and transition the subscope unit back to `planned`. However, topology, control-plane, scope, ancestor, or fundamental binding mismatches MUST immediately fail the run (`syncing` -> `failed`). Fundamental binding mismatches MUST NOT return the unit to `planned`. Set status to `applying` only after all checks pass.

## 3. Apply The Active Unit

Enter exclusively from `ready-to-apply` revalidation.

For each approved operation:
1. Recheck ancestor, identity, type, mode, size, and preimage hash without following links.
2. Perform race-resistant compare-and-swap using approved preimage.
3. Record actual postimage metadata and SHA-256.

Stop on any output deviation, secret exposure, or non-atomic failure. Transition to `applied` only when all actual outputs match manifest.

## 4. Failure And Rollback

On apply failure, transition to `rolling-back` and execute rollback plan. Require all safety primitives (descriptor-relative no-follow opens, regular-file checks, exclusive locks, atomic pointer replacements); fail closed immediately with evidence if any safety primitive is missing.

- Restore only targets matching recorded partial postimage.
- Use compare-and-swap with partial postimage as compare operand.
- Do not overwrite unexplained changes.

If fully restored, set `failed-rolled-back` and fail run.

If uncertain or partial:
1. Transition active subscope unit to `failed-partial`.
2. Append recovery evidence and persist a canonical recovery record in `.agent/temp/`.
3. Set `blocked_on_user: true` and log a Critical finding.
4. Route `failed-partial` state exclusively through an explicit recovery transition, preventing any `apply`, `acceptance`, or `commit` paths while filesystem state is uncertain.
5. Clear `blocked_on_user: true` ONLY after either a verified clean rollback is completed or the user provides an explicit approved recovery decision.

## 5. Verify Postimage

1. Verify actual outputs match approved outputs.
2. Ensure unselected paths, dirt, controls, and Git state changed only as declared.
3. Audit secrets, links, special files, and syntax using non-executing parsers.
4. Run read-only `/review` (resolve all Critical/Major findings).
5. Run `git diff --check --no-ext-diff --no-textconv`.
6. Confirm zero unauthorized VCS mutations.

Actual outputs MUST match approved postimage manifests exactly. Any unauthorized edit or postimage output mismatch invalidates apply and triggers [Failure And Rollback](#4-failure-and-rollback).

## 6. Post-Change Acceptance

Persist postimage evidence and diff (`applied -> awaiting-user-approval`).

Bind Acceptance Subject per [Sync Shared Approval Subjects](./_shared/sync-shared.md#4-approval-subjects). Request explicit user acceptance. Valid acceptance transitions unit to `awaiting-user-commit` (`blocked_on_user: true`).

## 7. Verify User Commit

Resume from Run artifact and user-reported commit SHA(s).

Requirements per modified Git root:
- Single non-merge commit descending directly from accepted base (`UNBORN` permits 0 parents).
- Changed paths match accepted outputs exactly.
- Tree entries match postimage bytes/types/modes.
- Verified post-commit HEAD equals reported SHA and has `merge-status=0`.
- **Revalidate Allowed Transition**: Verify the exact state transition from `acceptance-git`: the accepted output becomes exactly the reported commit (`changed-paths-sha256` matches `accepted-paths-sha256`, `commit-tree-sha256` matches `accepted-tree-sha256`), the commit has `accepted-base-head` as its direct parent, no extra paths enter the commit, pre-existing unrelated staged work remains unchanged in the index, pre-existing unrelated unstaged work remains unchanged in the worktree dirt, accepted output paths are no longer left staged or dirty, and current HEAD points to the verified commit.

Outcomes:
- All pass: Record SHA(s), extend checkpoint, set `committed`, activate next unit or cursor.
- Partial pass (repository mode): Set `partially-committed`, preserve passed evidence.
- Mismatch: Fail run.

## 8. Advance And Verify Run

After `committed` or `no-change`, revalidate cumulative checkpoint. If units remain, activate next `planned` unit.

When all units are terminal:
1. Verify full scope, checkpoints, commit digests, and residual drift.
2. Persist `final-verification` evidence.
3. Promote baseline via [Baseline Store](./sync-evidence.md#4-reusable-baseline-store).
4. Atomically set run status to `verified`, clear the `active_subscope` marker, and set `blocked_on_user: false`.

## 9. Executive Report

Lead with status (`awaiting commit`, `partially committed`, `verified`, `failed`). Report pair, topology, scope, Run ID, commits, actual vs approved diffs, baseline state, risk decision, build/test status (`not run per repo rule`), zero VCS mutations, and next action.
