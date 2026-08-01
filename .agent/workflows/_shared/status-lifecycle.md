---
description: Shared status, approval, lineage, and assumption contract for workflow artifacts
---

> **Estimated context: ~2.8K tokens**
> See [Workflow Operating Model](./workflow-operating-model.md).

## Lifecycle

```text
CLARIFY: draft -> clarifying -> draft-self-reviewed -> clarified
DEVELOP: ready-to-develop -> implementing -> implemented-pending-approval -> implemented
UPDATE : outlined -> planning -> ready -> plan-reviewed -> executing -> done -> reviewed -> verifying -> verified
         failed -> correcting -> done -> reviewed -> verifying -> verified | failed
SYNC RUN: discovering -> planned -> syncing -> verified | failed
SYNC SS : planned -> no-change
          planned -> ready-to-apply -> applying -> applied -> awaiting-user-approval
          -> awaiting-user-commit -> committed
          ready-to-apply -> planned
          applied | awaiting-user-approval | awaiting-user-commit -> planned
          applying -> rolling-back -> failed-rolled-back | failed-partial
          awaiting-user-commit -> partially-committed -> committed | failed
```

| Transition | Owner | Gate |
|---|---|---|
| `draft` -> `clarifying` -> `draft-self-reviewed` | `/clarify` | Questions, quality gates, and self-review pass. |
| `draft-self-reviewed` -> `clarified` | `/answer` | Current review and approval pass. |
| `clarified` -> `ready-to-develop` | `/answer` | Collision-safe DEVELOP handoff passes review. |
| `ready-to-develop` -> `implementing` | `/develop` | Freshness, completeness, and approval pass. |
| `implementing` -> `implemented-pending-approval` | `/develop` | Changes, verification, and walkthrough complete. |
| `implemented-pending-approval` -> `implemented` | `/develop` | User approves the final report. |
| `outlined` -> `planning` -> `ready` | `/update-plan` | Discovery and plan complete. |
| `ready` -> `plan-reviewed` | `/update` from `/review` | `transition_allowed: plan-reviewed`. |
| `plan-reviewed` -> `executing` -> `done` | `/update-execute` | Current apply approval, then all stages terminal. |
| `done` -> `reviewed` | `/update` from `/review` | `transition_allowed: reviewed`. |
| `reviewed` -> `verifying` -> `verified`/`failed` | `/update-verify` | Current run/scope/plan/output binding and verification verdict. |
| `failed` -> `correcting` -> `done` | `/update-execute` | Current correction preview, explicit approval, matching verification findings, and completed correction progress. |
| `discovering` -> `planned` -> `syncing` | `/sync` run | Physical parent/pair, overall frozen scope, ordered subscopes, and exactly one active unit are current. |
| `planned` -> `no-change` | `/sync` subscope | Current static evidence proves no material output; verified path state extends the cumulative checkpoint without approval or commit. |
| `planned` -> `ready-to-apply` | `/sync` subscope | Active-unit drift, exact preview/preimages, rollback, and a passing canonical pre-apply review record for that same preview revision are bound into the complete approval subject. |
| `ready-to-apply` -> `applying` -> `applied` | `/sync` subscope | Explicit current apply approval passes freshness; actual outputs exactly match the approved unit manifest. |
| `ready-to-apply` -> `planned` | `/sync` subscope | Active input/non-output state changed; invalidate approval append-only and create a fresh preview while the run remains `syncing`. |
| `applying` -> `rolling-back` -> `failed-rolled-back`/`failed-partial` | `/sync` subscope | Apply failed; containment-safe rollback either proves the exact pre-apply state or records Critical residual mutation. Both fail the run and are never resumed/reapplied. |
| `applied` -> `awaiting-user-approval` | `/sync` subscope | Static verification and immutable postimage evidence pass with no unresolved Critical or Major findings. |
| `awaiting-user-approval` -> `awaiting-user-commit` | `/sync` subscope | Exact postimage acceptance record is current. |
| `applied`/`awaiting-user-approval`/`awaiting-user-commit` -> `planned` | `/sync` subscope | A correction or pre-commit non-output change invalidates approval/acceptance before any verified commit; every current-run output must still equal its canonical postimage and becomes the next preview's preimage. Unexplained output dirt fails instead. |
| `awaiting-user-commit` -> `committed` | `/sync` subscope | Every required single non-merge user commit descends directly from its accepted base and has exactly the accepted raw path/tree delta. |
| `awaiting-user-commit` -> `partially-committed` -> `committed`/`failed` | `/sync` subscope | Repository roots are verified independently; passed-root evidence is immutable while remaining exact commits or explicit user-owned recovery are pending. `/sync` never rewrites or reapplies a committed root. |
| `planned`/`ready-to-apply`/`applied`/`awaiting-user-approval`/`awaiting-user-commit`/`partially-committed` -> `failed` | `/sync` subscope | An irrecoverable security, boundary, or commit mismatch stops the unit; no automatic re-entry is allowed. |
| `discovering`/`planned`/`syncing` -> `failed` | `/sync` run | A run-binding mismatch, unit failure, failed rollback, or abandoned partial commit blocks the run. |
| `syncing` -> `verified` | `/sync` run | Every unit is terminal as `committed` or `no-change`; final full-scope verification derives and promotes the complete baseline. |

`verified` is terminal only while its complete verification binding remains current. `/update` invalidates an output-only mismatch to `done` for review; a Run ID, requested/effective scope, or semantic-plan mismatch starts a fresh plan and never reuses prior approval or verification state.

SYNC run status and each `SS-NNN` status are separate. The run remains `syncing` across terminal units. A committed unit creates a Run-ID-bound cumulative checkpoint; a `no-change` unit extends it with current verified path state. The reusable pair baseline is derived from the final complete manifest only after terminal full-scope verification, then selected and atomically promoted through `/sync`'s stable pair/scope-keyed store. Any run, parent/root identity, scope, active-unit, checkpoint, review, approval, acceptance, commit-tree, baseline-pointer, or byte/state mismatch follows a declared invalidation/failure transition and never skips or reapplies a unit implicitly.

## Metadata

Use lowercase YAML fields; never replace `status`.

| Field | Meaning |
|---|---|
| `blocked_on_user` | A human decision blocks progress. |
| `needs_review` | Semantic content changed after review. |
| `stale` | A source or baseline changed. |
| `approval_required` | The next mutation lacks a current matching approval. |

Fail closed: initialize `needs_review: true` before creating or changing reviewable content. Clear it only after `/review` reports no unresolved Critical or Major findings. `approval_required: false` is valid only with the complete current record below.

For SYNC, set `blocked_on_user: true` while explicit apply approval, post-change acceptance, a user commit, or partial-commit recovery is pending; set it false otherwise. A failed state is terminal for that run and is not represented as merely waiting on the user.

## Approval Record

Use explicit approval unless this contract and the accepting workflow allow `ApprovalRecord=workflow-tolerated`. The latter never covers security-sensitive, destructive, VCS, failed/unclear, unresolved-input, unverified-assumption, temp-lifecycle-risk, or final user-approval gates.

Required fields:

| Field | Value |
|---|---|
| `approval_record` | `explicit approval recorded` or `ApprovalRecord=workflow-tolerated` |
| `approval_scope` | Exact mutation and bounded targets |
| `approval_subject` | Approved artifact or persisted preview |
| `approval_subject_sha256` | Semantic-subject or preview SHA-256 |
| `approval_preview_sha256` | Exact diff/preview SHA-256; `N/A` when absent |
| `approval_producer` | User or allowed workflow |
| `approval_recorded_at` | ISO 8601 timestamp |
| `approval_risk_decision` | Priority Stack result and accepted residual risk |
| `approval_envelope_sha256` | Digest binding every field above |

### Semantic Subject

For YAML-frontmatter artifacts that store their approval, hash raw artifact bytes after removing only top-level lifecycle fields (`status`, `clarified`, `implemented`, `blocked_on_user`, `needs_review`, `stale`) and `approval_*` lines with their indented continuations. Preserve every other byte and order. Workflow-specific projections, such as `/update`'s semantic plan, override this rule.

### Approval Envelope

Start with ASCII `DRN-APPROVAL`, NUL, and byte `0x01`. Append, in table order excluding only `approval_envelope_sha256`, each field name and value as `name_length:uint64-be | name:utf8 | value_length:uint64-be | value:utf8`. Use lowercase hex digests and the literal `N/A`. `approval_envelope_sha256` is the lowercase SHA-256 of these bytes.

The user approves the human-readable subject/preview, scope, and risk—not a digest. On confirmation, recompute every digest, record all fields, and set `approval_required: false`. Any envelope-input change invalidates it. Set `approval_required: true`, record reason/time, retain the superseded record as append-only audit history, review changed semantic content, and obtain new approval.

`/sync` requires two separate complete records per mutating subscope. Each workflow-specific binding is raw content of an immutable UTF-8 subject whose exact SHA-256 is stored in `approval_subject_sha256`; canonical binary manifest digests referenced by that subject bind raw/non-UTF path evidence. Use only the fixed shared Approval Envelope fields above—never append unhashed workflow metadata.

- The apply subject binds Run ID, `SS-NNN`, endpoint topology, owning Git state, direction, and canonical scope/action, baseline/checkpoint, control, input, preview/preimage, rollback, and risk evidence.
- The acceptance subject binds Run ID, `SS-NNN`, endpoint topology, canonical postimage/residual evidence, accepted base HEAD/ref or `UNBORN`, pre-transition index/dirty state, control evidence, and risk. The shared producer and recorded-at fields bind user and timestamp.

The records are distinct and append-only; acceptance never reuses apply approval. Any accepted postimage or bound state change follows the declared invalidation transition before the user-commit gate, except the workflow-declared exact staging/single-commit transition.

## Lineage

CLARIFY descendants may record `iteration`, `previous_artifact`, `previous_status`, `previous_updated`, `previous_sha256`, `previous_develop_artifact`, `previous_develop_sha256`, `previous_walkthrough_artifact`, and `previous_commit`.

- Accept supplied or unambiguous name-versioned artifacts and refs; require hashes only when a listed hash drives freshness.
- Higher iteration, then newer timestamp, supersedes an ancestor. Branch only with user confirmation.
- `### Enriched Lineage Snapshot` may summarize prior CLARIFY, DEVELOP, walkthrough, and commit evidence. It never replaces freshness, approval, review, optimize, or develop gates.

## Assumptions And Re-entry

`[ASSUMPTION - unverified]` blocks `draft-self-reviewed`, `clarified`, `ready-to-develop`, `implementing`, `implemented-pending-approval`, every mutating/acceptance SYNC state, and terminal SYNC verification. `[ASSUMPTION - accepted]` is allowed only in the Risk Register with source and mitigation; it never bypasses Security, Correctness, criteria, or approval.

Resume from `status` and flags. For SYNC, resolve both run status and the one active subscope. Revalidate source hashes, review state, approval/acceptance envelopes, checkpoints, commit evidence, and blockers before mutation.
