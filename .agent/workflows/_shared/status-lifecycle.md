---
description: Shared status, approval, lineage, and assumption contract for workflow artifacts
---

> **Estimated context: ~1.4K tokens**
> See [Workflow Operating Model](./workflow-operating-model.md).

## Lifecycle

```text
CLARIFY: draft -> clarifying -> draft-self-reviewed -> clarified
DEVELOP: ready-to-develop -> implementing -> implemented-pending-approval -> implemented
UPDATE : outlined -> planning -> ready -> plan-reviewed -> executing -> done -> reviewed -> verifying -> verified
         failed -> verifying -> verified | failed
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
| `reviewed`/`failed` -> `verifying` -> `verified`/`failed` | `/update-verify` | Verification verdict. |

## Metadata

Use lowercase YAML fields; never replace `status`.

| Field | Meaning |
|---|---|
| `blocked_on_user` | A human decision blocks progress. |
| `needs_review` | Semantic content changed after review. |
| `stale` | A source or baseline changed. |
| `approval_required` | The next mutation lacks a current matching approval. |

Fail closed: initialize `needs_review: true` before creating or changing reviewable content. Clear it only after `/review` reports no Critical finding. `approval_required: false` is valid only with the complete current record below.

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

## Lineage

CLARIFY descendants may record `iteration`, `previous_artifact`, `previous_status`, `previous_updated`, `previous_sha256`, `previous_develop_artifact`, `previous_develop_sha256`, `previous_walkthrough_artifact`, and `previous_commit`.

- Accept supplied or unambiguous name-versioned artifacts and refs; require hashes only when a listed hash drives freshness.
- Higher iteration, then newer timestamp, supersedes an ancestor. Branch only with user confirmation.
- `### Enriched Lineage Snapshot` may summarize prior CLARIFY, DEVELOP, walkthrough, and commit evidence. It never replaces freshness, approval, review, optimize, or develop gates.

## Assumptions And Re-entry

`[ASSUMPTION - unverified]` blocks `draft-self-reviewed`, `clarified`, `ready-to-develop`, `implementing`, and `implemented-pending-approval`. `[ASSUMPTION - accepted]` is allowed only in the Risk Register with source and mitigation; it never bypasses Security, Correctness, criteria, or approval.

Resume from `status` and flags. Revalidate source hashes, review state, approval envelope, and blockers before mutation.
