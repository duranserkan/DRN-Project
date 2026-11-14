---
description: Shared status, approval, lineage, and assumption contract for workflow artifacts
---

# Status Lifecycle

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
          failed-partial -> recovering -> failed-rolled-back | failed-partial
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
| `implemented-pending-approval` -> `implementing` | `/develop` | Explicitly authorized corrections pass freshness, review and approval gates. |
| `outlined` -> `planning` -> `ready` | `/update-plan` | Discovery and plan complete. |
| `ready` -> `plan-reviewed` | `/update` from `/review` | `transition_allowed: plan-reviewed`. |
| `plan-reviewed` -> `executing` -> `done` | `/update-execute` | Current apply approval, all stages terminal. |
| `done` -> `reviewed` | `/update` from `/review` | `transition_allowed: reviewed`. |
| `reviewed` -> `verifying` -> `verified`/`failed` | `/update-verify` | Binding and verification verdict pass. |
| `failed` -> `correcting` -> `done` | `/update-execute` | Correction preview, approval, findings resolution. |
| `discovering` -> `planned` -> `syncing` | `/sync` run | Physical parent/pair, scope, ordered subscopes bound. |
| `planned` -> `no-change` | `/sync` subscope | Static evidence proves zero output. |
| `planned` -> `ready-to-apply` | `/sync` subscope | Drift, preview, preimages, rollback plan, and review pass. Await Apply approval. |
| `ready-to-apply` -> `applying` -> `applied` | `/sync` subscope | Executor revalidates explicit Apply approval, persists pre-write marker, and matches outputs to manifest. |
| `ready-to-apply`/pre-write `applying` -> `planned` | `/sync` subscope | Input/non-output changed before pre-write marker. Invalidate approval append-only. |
| `applying` -> `rolling-back` -> `failed-*` | `/sync` subscope | Apply failed. Safe rollback executed or partial failure logged. |
| `failed-partial` -> `recovering` -> `failed-rolled-back`/`failed-partial` | `/sync` subscope | Explicit Recovery approval binds exact plan. Verified rollback terminates safely. Residual uncertainty reblocks. |
| `applied` -> `awaiting-user-approval` | `/sync` subscope | Verification and postimage evidence pass `/review`. |
| `awaiting-user-approval` -> `awaiting-user-commit` | `/sync` subscope | Postimage acceptance record current. |
| `applied`/`awaiting-*` -> `planned` | `/sync` subscope | Pre-commit change invalidates approval before commit. |
| `awaiting-user-commit` -> `committed` | `/sync` subscope | Single non-merge user commit matches accepted tree delta. |
| `awaiting-user-commit` -> `partially-committed` | `/sync` subscope | Independent Git roots verified. Passed roots immutable. |
| any mutating state -> `failed` | `/sync` subscope | Irrecoverable security/boundary failure stops unit. |
| `discovering`/`planned`/`syncing` -> `failed` | `/sync` run | Topology mismatch, unit failure, or corrupt baseline stops run. |
| `syncing` -> `verified` | `/sync` run | All units terminal (`committed`/`no-change`). Baseline promoted. |

`verified` is terminal only while its complete verification binding remains current.

For `/update`, output-only mismatch returns to `done` for review. Run ID, requested/effective scope, or semantic-plan mismatch starts a fresh `outlined`/`planning` plan without prior approval or verification state.

For `/sync`, mismatched evidence transitions to `failed`. Changed live state or invalid baseline/topology starts a fresh `discovering` run. Never reopen a verified run as `planned` or `syncing`.

## Metadata

Use lowercase YAML fields. Never replace `status`.

| Field | Meaning |
|---|---|
| `blocked_on_user` | Human decision blocks progress. |
| `needs_review` | Semantic content changed after review. |
| `stale` | Source or baseline changed. |
| `approval_required` | Next mutation lacks matching approval. |

Default `needs_review: true`. Clear only after `/review` passes.

For SYNC, set `blocked_on_user: true` while apply approval, acceptance, commit, or recovery is pending.

## Approval Record

Use explicit approval by default. `ApprovalRecord=workflow-tolerated` requires this contract and both producer and consumer to opt in for the exact bounded scope. It never satisfies security-sensitive, destructive, VCS, failed/unclear, unresolved-input, unverified-assumption, temp-artifact lifecycle-risk, or final user-approval gates, including `implemented` and final plan/output completion.

`/sync` rejects tolerated approval. Apply, Acceptance, and Recovery require separate explicit user records. Rejected/non-explicit records remain `blocked_on_user: true` and cannot enter `applying`, `awaiting-user-commit`, or `recovering`.

Required fields:
- `approval_record`: `explicit approval recorded` or `ApprovalRecord=workflow-tolerated`
- `approval_scope`: Exact mutation and bounded targets
- `approval_subject`: Approved artifact or preview path
- `approval_subject_sha256`: Subject SHA-256
- `approval_preview_sha256`: Workflow-mapped human-readable preview or diff
  artifact SHA-256 (`N/A` only when the workflow has no separate preview
  evidence)
- `approval_producer`: User or allowed workflow
- `approval_recorded_at`: ISO 8601 timestamp
- `approval_risk_decision`: Priority Stack result & residual risk
- `approval_envelope_sha256`: Digest binding the eight envelope-input fields

### Semantic Subject

For YAML-frontmatter artifacts that store their approval, hash raw artifact bytes after removing only top-level lifecycle fields (`status`, `clarified`, `implemented`, `blocked_on_user`, `needs_review`, `stale`) and `approval_*` lines with their indented continuations. Preserve every other byte and order.

### Envelope Calculation

Calculate approval-envelope SHA-256 from the first eight fields above in listed order.
- Validate raw field values before serialization. Every value must be valid UTF-8 and contain no CR, LF, or NUL characters.
- Encode structured risk information as compact single-line JSON when needed.
- Serialize each field as exact UTF-8 `name=value` bytes followed by one LF (`\n`). Include exactly one LF after the eighth field and perform no other value normalization.

Retain the recorded envelope SHA-256 directly in the approval record. Never hash the containing approval record or history.

The user approves the readable subject/preview, scope, and risk, not a digest. Recompute digests, record all fields, then clear `approval_required`. Any envelope-input change invalidates the record. Set `approval_required: true`, record reason/time, retain superseded records append-only outside the subject, review changed content, and obtain current approval. External history avoids changing the semantic subject while recording invalidation.

`/sync` requires separate Apply and Acceptance records per mutating subscope, and a Recovery record when applicable. Retain superseded records append-only.

### CAD Scoped Authorization

For `/clarify`, `/answer`, and `/develop`, an explicit user instruction may authorize a bounded chain and its corrective iterations upfront. Record its wording and provenance in the artifact. Reuse that authority only while scope, constraints, and risk remain covered and no later instruction revokes or conflicts with it. Do not request the same permission again.

Content changes still invalidate the envelope. Review the current artifact and recompute every digest before advancing. Existing authority may authorize the new envelope only when it explicitly covers the derived content or corrections. Otherwise ask. Never relabel explicit authorization as workflow-tolerated.

This rule does not grant execution, destructive, security-sensitive, or VCS permission, waive a failed gate, or approve final results. It does not replace exact-preview approval in `/optimize`, `/update`, or `/sync`. Continue an already authorized CAD chain after presenting its artifacts. Stop at an unapproved boundary.

## Lineage & Assumptions

### Lineage Metadata

For clarification loops, use these keys for lineage and supersession:

| Key | Meaning |
|---|---|
| `iteration` | Current iteration number in the lineage |
| `previous_artifact` | Prior `CLARIFY-*` input artifact |
| `previous_status` | Prior artifact status at creation |
| `previous_updated` | Prior artifact timestamp or filesystem mtime used for freshness |
| `previous_sha256` | Prior artifact SHA-256 at creation |
| `previous_develop_artifact` | Prior `DEVELOP-*` artifact summarized into the enriched lineage snapshot, if supplied/unambiguous |
| `previous_develop_sha256` | Prior `DEVELOP-*` SHA-256 when summarized |
| `previous_walkthrough_artifact` | Prior walkthrough artifact summarized into the enriched lineage snapshot, if supplied/unambiguous |
| `previous_commit` | Prior commit/ref summarized into the enriched lineage snapshot, if supplied/unambiguous |

Apply these shared lineage rules unless a workflow names a stricter local gate:

- **Evidence & Selection**: Accept explicit name-versioned artifacts and commits/refs when supplied or unambiguous. Require hashes only when a listed `*_sha256` key drives freshness. Revalidate source hashes and freshness before mutation.
- **Supersession & Branch Confirmation**: Same-lineage descendants supersede `previous_artifact` by higher `iteration`, then newer timestamp. Historical inspection is evidence only. Resuming a superseded artifact as a branch point requires explicit user confirmation.
- **Snapshot Boundary**: `### Enriched Lineage Snapshot` lets a new clarification iteration stand alone for `/answer`. It may summarize prior `CLARIFY-*`, matching `DEVELOP-*`, walkthrough, and commit evidence. It never replaces `source_*` freshness checks, approval records, `/review`, `/optimize`, or `/develop` gates.

### Assumptions & Re-entry

`[ASSUMPTION - unverified]` blocks all mutating, terminal, review-ready, verification, and approval/commit-waiting states across CAD, UPDATE, and SYNC. `[ASSUMPTION - accepted]` is allowed only in the Risk Register with explicit mitigation and source. It never bypasses Security, Correctness, testable acceptance criteria, or approval records.

Resume from `status` and metadata flags. Revalidate source hashes, review state, approval envelope, and blockers before mutation.
