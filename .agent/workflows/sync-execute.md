---
description: Apply one approved sync subscope, verify its postimage, and obtain acceptance
---

# Sync Execute

> **Trigger**: Active [`/sync`](./sync.md) unit is `ready-to-apply`, `applied`, `awaiting-user-approval`, `awaiting-user-commit`, or `partially-committed` with the matching current approval/acceptance or user notification; or all units are terminal for final verification.
> **Exit**: the next unit is activated, or the run is `verified`/`failed`.
> See [Operating Model](./_shared/workflow-operating-model.md), [Lifecycle](./_shared/status-lifecycle.md), [Evidence Protocol](./sync-evidence.md), and [`/review`](./review.md).

## 1. Revalidate Approval

Resume only from the explicit Run ID artifact. Before mutation:

1. Recompute the approval envelope, apply-subject hash, preview hash, and every referenced evidence hash.
2. Revalidate physical roots, topology, control plane, Git administrative state, target ancestors, scope, active `SS-NNN`, inputs, preimages, and immutable non-output state.
3. Confirm approval covers the exact target paths, operations, outputs, and residual risk.
4. Confirm the preview's review has no unresolved Critical or Major findings.

An affected input or non-output change returns the unit to `planned` and invalidates approval. A fundamental pair, topology, control-plane, or scope mismatch fails the run. Preserve superseded records as history.

Set `applying` only after every binding passes.

## 2. Apply The Active Unit

For each approved action:

1. Immediately recheck target ancestor, presence, identity, type, mode, size, and preimage hash without following links.
2. Abort before writing if the target or input differs from the approved evidence.
3. Apply the smallest declared operation through a race-resistant compare-and-swap that uses the approved preimage as its compare operand. Use exclusive creation for absent targets and conditional atomic replace/delete for present targets; fail closed before writing if those primitives are unavailable. Do not traverse links or cross the verified root/mount.
4. Preserve target-specific adaptations and expected variances.
5. Record the actual postimage presence, identity, type, mode, size, SHA-256, and operation result.

Stop on undeclared output, secret exposure, protection-rule failure, stale evidence, or indeterminate result. Never continue to another unit.

Transition to `applied` only when every actual output matches the approved manifest.

## 3. Failure And Rollback

On a known apply failure, enter `rolling-back` and follow the approved rollback plan:

- Restore only targets that still match the recorded partial postimage.
- Use the same boundary, identity, no-follow, and compare-and-swap protections as apply, with the recorded partial postimage as the compare operand.
- Do not overwrite concurrent or unexplained changes.

If the complete pre-apply state is restored, set `failed-rolled-back` and fail the run. If any target is uncertain or cannot be restored exactly, set `failed-partial`, record a Critical finding, stop, and request recovery direction. Failed runs are never resumed or silently reapplied.

## 4. Verify Postimage

For the active applied unit:

1. Re-enumerate the bounded manifest and prove actual outputs equal the approved outputs.
2. Prove sources, unselected paths, adopted/disjoint dirt, control files, and Git roots changed only as declared.
3. Recheck secrets, links, special files, binaries/generated policy, and structured-file syntax with non-executing parsers.
4. Run read-only `/review` on the active diff; resolve every Critical and Major finding.
5. Run `git diff --check --no-ext-diff --no-textconv` in affected Git roots.
6. Reconcile residual drift and expected variances.
7. Confirm before/after Git administrative evidence matches and no VCS state was mutated.
8. Record build/test commands as executed only when explicitly authorized; otherwise state `not run per repo rule`.

If correction requires a target edit, invalidate approval and acceptance, return the unit to `planned`, and create a fresh `/sync` preview. If output differs without a valid workflow transition, fail the run.

## 5. Post-Change Acceptance

Persist immutable postimage evidence and an exact post-change diff. The acceptance subject must bind the Run ID, `SS-NNN`, pair/topology and owning Git state, governing control hashes, postimage, residual drift, accepted base HEAD/ref and clean/disjoint state, Priority Stack decision, and residual risk.

Run `/review` on the acceptance evidence when the post-change review is not already bound to the same bytes. Critical or Major findings block acceptance.

Transition `applied -> awaiting-user-approval`. Request explicit acceptance of the human-readable postimage, residual variance, commit boundary, and risk through a second complete shared Approval Record. Apply approval never substitutes for acceptance.

Before accepting, recompute all bindings. A correctable state change returns the unit to `planned` only when every current output still matches its canonical postimage. Unexplained output mutation fails the run.

Valid acceptance transitions the unit to `awaiting-user-commit`. Set `blocked_on_user: true` while acceptance or commit is pending, then stop. `/sync` never commits for the user.

## 6. Verify User Commit

Resume from the Run artifact and user-reported commit SHA(s). Revalidate acceptance, postimage, Git base, disjoint state, pair/topology, scope, and control plane. Use the same read-only Git protections; never mutate VCS.

For each modified Git root, require one user-created non-merge commit whose single parent is the accepted base (`UNBORN` permits no parent), changed paths exactly match accepted outputs, tree entries match postimage bytes/types/modes, and current HEAD/ref plus index/worktree preserve disjoint dirt.

Project mode requires one shared-repository commit; repository mode requires one per modified endpoint. Outcomes:

- All pass: set `committed`, record SHA(s), extend the checkpoint.
- Some repository roots pass: set `partially-committed`, preserve passed evidence, block on exact completion/recovery, and never reapply a committed root.
- Accepted base, path, bytes, topology, or Git state differs: fail the run unless the shared lifecycle explicitly permits a fresh pre-commit correction from unchanged canonical outputs.

Do not accept a nearby commit, later descendant, merge commit, or equivalent-looking tree.

## 7. Advance And Verify Run

After `committed` or verified `no-change`, revalidate the cumulative checkpoint. Activate exactly one remaining unit and return to `/sync`; prior previews and approvals do not carry forward.

When all units are terminal, verify the full current scope: pair/topology/direction/mappings/control binding, one manifest record per scoped path, committed/no-change checkpoints, expected variances, no unauthorized output or Git mutation, no unsafe file/security condition, and no unresolved Critical/Major finding.

Derive the baseline only from this verified manifest. Promote it through the [baseline protocol](./sync-evidence.md#4-reusable-baseline-store). Concurrent or malformed pointer state stops promotion. Set the run `verified` only after promotion succeeds; otherwise fail with the violated invariant and recovery options.

## 8. Executive Report

Lead with `awaiting commit`, `partially committed`, `verified`, or `failed`. Report pair/topology, scope, Run ID, each unit/commit, actual versus approved paths, checkpoint/baseline, expected variances, TRIZ/Priority Stack decision, residual risk, build/test status, zero VCS mutations, and the exact next action.
