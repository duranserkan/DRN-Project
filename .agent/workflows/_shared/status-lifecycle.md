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
| `outlined` -> `planning` -> `ready` | `/update-plan` | Discovery and plan complete. |
| `ready` -> `plan-reviewed` | `/update` from `/review` | `transition_allowed: plan-reviewed`. |
| `plan-reviewed` -> `executing` -> `done` | `/update-execute` | Current apply approval, all stages terminal. |
| `done` -> `reviewed` | `/update` from `/review` | `transition_allowed: reviewed`. |
| `reviewed` -> `verifying` -> `verified`/`failed` | `/update-verify` | Binding and verification verdict pass. |
| `failed` -> `correcting` -> `done` | `/update-execute` | Correction preview, approval, findings resolution. |
| `discovering` -> `planned` -> `syncing` | `/sync` run | Physical parent/pair, scope, ordered subscopes bound. |
| `planned` -> `no-change` | `/sync` subscope | Static evidence proves zero output. |
| `planned` -> `ready-to-apply` | `/sync` subscope | Active drift, preview, preimages, rollback plan, and passing `/review` complete; awaiting Apply approval. |
| `ready-to-apply` -> `applying` -> `applied` | `/sync` subscope | Explicit Apply approval granted; `/sync-execute` revalidates it, persists the pre-write marker, and actual outputs match manifest. |
| `ready-to-apply`/pre-write `applying` -> `planned` | `/sync` subscope | Input/non-output changed before the pre-write marker; invalidate approval append-only. |
| `applying` -> `rolling-back` -> `failed-*` | `/sync` subscope | Apply failed; safe rollback executed or partial failure logged. |
| `failed-partial` -> `recovering` -> `failed-rolled-back`/`failed-partial` | `/sync` subscope | Explicit Recovery approval binds an exact recovery plan; verified rollback terminates safely, while residual uncertainty reblocks recovery. |
| `applied` -> `awaiting-user-approval` | `/sync` subscope | Verification and postimage evidence pass `/review`. |
| `awaiting-user-approval` -> `awaiting-user-commit` | `/sync` subscope | Postimage acceptance record current. |
| `applied`/`awaiting-*` -> `planned` | `/sync` subscope | Pre-commit change invalidates approval before commit. |
| `awaiting-user-commit` -> `committed` | `/sync` subscope | Single non-merge user commit matches accepted tree delta. |
| `awaiting-user-commit` -> `partially-committed` | `/sync` subscope | Independent Git roots verified; passed roots immutable. |
| any mutating state -> `failed` | `/sync` subscope | Irrecoverable security/boundary failure stops unit. |
| `discovering`/`planned`/`syncing` -> `failed` | `/sync` run | Topology mismatch, unit failure, or corrupt baseline stops run. |
| `syncing` -> `verified` | `/sync` run | All units terminal (`committed`/`no-change`); baseline promoted. |

`verified` is terminal only while its complete verification binding remains current.

For `/update` artifacts, an output-only mismatch invalidates `/update` to `done` for review (`verified` -> `done`); a Run ID, requested/effective scope, or semantic-plan mismatch invalidates the plan completely and starts a fresh plan (`outlined`/`planning`), never reusing prior approval or verification state.

For `/sync` run artifacts, `verified` is terminal while binding is current. Mismatched evidence transitions to `failed`; changed live state or invalid baseline/topology starts a fresh `discovering` run. Never reopen a verified SYNC run as `planned` or `syncing`.

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

Use explicit approval by default. Use `ApprovalRecord=workflow-tolerated` ONLY when BOTH this contract and the accepting/consuming workflow explicitly opt in and declare the exception for the exact bounded scope. `/sync` explicitly rejects `ApprovalRecord=workflow-tolerated` and requires explicit user approval for Apply, Acceptance, and Recovery records; rejected or non-explicit records MUST remain blocked (`blocked_on_user: true`) and MUST NEVER transition to `applying`, `awaiting-user-commit`, or `recovering`.

`ApprovalRecord=workflow-tolerated` NEVER satisfies:
- Workflows that have not explicitly opted in and accepted workflow-tolerated approval (including `/sync`, which strictly rejects it)
- Security-sensitive gates
- Destructive mutation gates
- VCS gates (e.g., commit verification or acceptance)
- Failed or unclear gates (e.g., failed test or review gates)
- Unresolved-input gates
- Unverified-assumption gates
- Temp-artifact lifecycle-risk gates
- Final user-approval gates (including `status: implemented` or final plan/output completion)

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

The user approves the human-readable subject/preview, scope, and risk—not a digest. On confirmation, recompute every digest, record all fields, and set `approval_required: false`. Any envelope-input change invalidates it. Set `approval_required: true`, record reason/time, retain superseded records as append-only audit history, review changed semantic content, and obtain new approval.

`/sync` requires separate Apply and Acceptance records per mutating subscope, and a Recovery record when applicable. Retain superseded records append-only.

## Lineage & Assumptions

### Lineage Metadata

`CLARIFY-*` artifacts may use these keys when a new clarification loop starts from an earlier artifact. Use them for supersession and lineage tracking:

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
- **Supersession & Branch Confirmation**: Same-lineage descendants supersede `previous_artifact` by higher `iteration`, then newer timestamp. Treat superseded artifacts as evidence or branch points only with explicit user confirmation.
- **Snapshot Boundary**: `### Enriched Lineage Snapshot` lets a new clarification iteration stand alone for `/answer`. It may summarize prior `CLARIFY-*`, matching `DEVELOP-*`, walkthrough, and commit evidence. It never replaces `source_*` freshness checks, approval records, `/review`, `/optimize`, or `/develop` gates.

### Assumptions & Re-entry

`[ASSUMPTION - unverified]` blocks all mutating and terminal states across CAD, UPDATE, and SYNC lifecycles (including `draft-self-reviewed`, `clarified`, `ready-to-develop`, `implementing`, `implemented-pending-approval`, `plan-reviewed`, `executing`, `done`, `reviewed`, `verifying`, `ready-to-apply`, `applying`, `applied`, `awaiting-user-approval`, `awaiting-user-commit`, `committed`, and terminal `verified`). `[ASSUMPTION - accepted]` is allowed only in the Risk Register with explicit mitigation and source; it never bypasses Security, Correctness, testable acceptance criteria, or approval records.

Resume from `status` and metadata flags. Revalidate source hashes, review state, approval envelope, and blockers before mutation.
