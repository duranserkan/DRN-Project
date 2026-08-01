---
description: Shared status, approval, lineage, and assumption contract for workflow artifacts
---

# Status Lifecycle

> **Estimated context: ~1.5K tokens**
> See [Workflow Operating Model](./workflow-operating-model.md) and [Sync Shared](./sync-shared.md).

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
| `draft` -> `clarifying` -> `draft-self-reviewed` | `/clarify` | Questions, quality gates, self-review pass. |
| `draft-self-reviewed` -> `clarified` | `/answer` | Review and approval pass. |
| `clarified` -> `ready-to-develop` | `/answer` | Collision-safe DEVELOP handoff passes review. |
| `ready-to-develop` -> `implementing` | `/develop` | Freshness, completeness, approval pass. |
| `implementing` -> `implemented-pending-approval` | `/develop` | Changes, verification, walkthrough complete. |
| `implemented-pending-approval` -> `implemented` | `/develop` | User approves final report. |
| `outlined` -> `planning` -> `ready` | `/update-plan` | Discovery and plan complete. |
| `ready` -> `plan-reviewed` | `/update` from `/review` | `transition_allowed: plan-reviewed`. |
| `plan-reviewed` -> `executing` -> `done` | `/update-execute` | Current apply approval, all stages terminal. |
| `done` -> `reviewed` | `/update` from `/review` | `transition_allowed: reviewed`. |
| `reviewed` -> `verifying` -> `verified`/`failed` | `/update-verify` | Binding and verification verdict pass. |
| `failed` -> `correcting` -> `done` | `/update-execute` | Correction preview, approval, findings resolution. |
| `discovering` -> `planned` -> `syncing` | `/sync` run | Physical parent/pair, scope, ordered subscopes bound. |
| `planned` -> `no-change` | `/sync` subscope | Static evidence proves zero output. |
| `planned` -> `ready-to-apply` | `/sync` subscope | Active drift, preview, preimages, rollback, passing `/review`. |
| `ready-to-apply` -> `applying` -> `applied` | `/sync` subscope | Apply approval passes freshness; actual outputs match manifest. |
| `ready-to-apply` -> `planned` | `/sync` subscope | Input/non-output changed; invalidate approval append-only. |
| `applying` -> `rolling-back` -> `failed-*` | `/sync` subscope | Apply failed; safe rollback executed or partial failure logged. |
| `applied` -> `awaiting-user-approval` | `/sync` subscope | Verification and postimage evidence pass `/review`. |
| `awaiting-user-approval` -> `awaiting-user-commit` | `/sync` subscope | Postimage acceptance record current. |
| `applied`/`awaiting-*` -> `planned` | `/sync` subscope | Pre-commit change invalidates approval before commit. |
| `awaiting-user-commit` -> `committed` | `/sync` subscope | Single non-merge user commit matches accepted tree delta. |
| `awaiting-user-commit` -> `partially-committed` | `/sync` subscope | Independent Git roots verified; passed roots immutable. |
| any mutating state -> `failed` | `/sync` subscope | Irrecoverable security/boundary failure stops unit. |
| `discovering`/`planned`/`syncing` -> `failed` | `/sync` run | Topology mismatch, unit failure, or corrupt baseline stops run. |
| `syncing` -> `verified` | `/sync` run | All units terminal (`committed`/`no-change`); baseline promoted. |

`verified` is terminal while binding is current. Mismatched evidence transitions to `failed`; changed live state starts a fresh `discovering` run. Never reopen a verified run as `planned` or `syncing`.

## Metadata

Lowercase YAML fields; never replace `status`.

| Field | Meaning |
|---|---|
| `blocked_on_user` | Human decision blocks progress. |
| `needs_review` | Semantic content changed after review. |
| `stale` | Source or baseline changed. |
| `approval_required` | Next mutation lacks matching approval. |

Default `needs_review: true`. Clear only after `/review` passes.

For SYNC, set `blocked_on_user: true` while apply approval, acceptance, commit, or recovery is pending.

## Approval Record

Require explicit approval unless `ApprovalRecord=workflow-tolerated`.

Fields:
- `approval_record`: `explicit approval recorded` or `ApprovalRecord=workflow-tolerated`
- `approval_scope`: Exact mutation and targets
- `approval_subject`: Approved artifact or preview path
- `approval_subject_sha256`: Subject SHA-256
- `approval_preview_sha256`: Diff SHA-256 (`N/A` if absent)
- `approval_producer`: User or allowed workflow
- `approval_recorded_at`: ISO 8601 timestamp
- `approval_risk_decision`: Priority Stack result & residual risk
- `approval_envelope_sha256`: Digest binding all fields

### Semantic Subject

For YAML-frontmatter artifacts that store their approval, hash raw artifact bytes after removing only top-level lifecycle fields (`status`, `clarified`, `implemented`, `blocked_on_user`, `needs_review`, `stale`) and `approval_*` lines with their indented continuations. Preserve every other byte and order.

### Envelope Calculation

Header: `DRN-APPROVAL` | NUL | `0x01`. Append each of the first 8 fields above in table order (excluding `approval_envelope_sha256`): `name_length:uint64-be | name:utf8 | value_length:uint64-be | value:utf8`. `approval_envelope_sha256` is lowercase SHA-256 of these bytes.

`/sync` requires two separate records per mutating subscope (Apply and Acceptance subjects). Retain superseded records append-only.

## Lineage & Assumptions

CLARIFY descendants record iteration and ancestor SHAs.

`[ASSUMPTION - unverified]` blocks all mutating and terminal states. `[ASSUMPTION - accepted]` is restricted to Risk Register with explicit mitigation.
