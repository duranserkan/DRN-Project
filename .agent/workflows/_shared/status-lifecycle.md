---
description: Shared status lifecycle for agent workflow artifacts
---

> **Estimated context: ~0.4K tokens**
> See also: [Workflow Operating Model](./workflow-operating-model.md)

## Status Lifecycle

```text
CLARIFY-* : draft -> clarifying -> draft-self-reviewed -> clarified
DEVELOP-* : ready-to-develop -> implemented
UPDATE    : outlined -> planning -> ready -> plan-reviewed -> executing -> done -> reviewed -> verifying -> verified
UPDATE    : failed -> verifying -> verified | failed
```

### Status Transitions

| Status | Artifact | Trigger | Owner |
|---|---|---|
| `draft` | `CLARIFY-*` | Document created (§2) | `/clarify` |
| `clarifying` | `CLARIFY-*` | First question round begins (§5) | `/clarify` |
| `draft-self-reviewed` | `CLARIFY-*` | Gates and checklist pass (§9) | `/clarify` |
| `clarified` | `CLARIFY-*` | Approval criteria met (§6) | `/answer` |
| `ready-to-develop` | `DEVELOP-*` | Development document produced (§7) | `/answer` |
| `implemented` | `DEVELOP-*` | User approves final report (§7) | `/develop` |
| `outlined` | `update-plan.md` | Initial update plan shell created | `/update-plan` |
| `planning` | `update-plan.md` | Discovery/detailing in progress | `/update-plan` |
| `ready` | `update-plan.md` | Plan has resolved stages and awaits review | `/update-plan` |
| `plan-reviewed` | `update-plan.md` | `/review` reports `transition_allowed: plan-reviewed` | `/update` |
| `executing` | `update-plan.md` | First execution stage starts | `/update-execute` |
| `done` | `update-plan.md` | All in-scope execution stages complete | `/update-execute` |
| `reviewed` | `update-plan.md` | `/review` reports `transition_allowed: reviewed` | `/update` |
| `verifying` | `update-plan.md`, `update-verify-progress.md` | Verification starts or resumes | `/update-verify` |
| `verified` | `update-plan.md`, `update-verify-progress.md` | Verification verdict passes | `/update-verify` |
| `failed` | `update-plan.md`, `update-verify-progress.md` | Verification verdict fails | `/update-verify` |

### Metadata Flags

Flags are lowercase YAML metadata and do not replace `status`.

| Flag | Meaning | Clear When |
|---|---|---|
| `blocked_on_user: true` | Human decision is required before the owner can advance status | The decision is recorded |
| `needs_review: true` | The artifact changed after its last review | `/review` passes with no critical findings |
| `stale: true` | Source artifact changed after this artifact was produced | The artifact is regenerated or revalidated |
| `approval_required: true` | The next step mutates source/state, VCS, or risk-bearing scope and no current approval record has been captured | `approval_record` and `approval_scope` capture explicit approval or a valid shared approval record for the exact next mutation |

Artifacts that carry `approval_required` must also carry `approval_record` and `approval_scope` when the flag is cleared. `approval_required: false` without a current matching approval record is an unresolved approval gate.

### Lineage Metadata

`CLARIFY-*` artifacts use optional lineage keys when a new clarification loop starts from an earlier artifact. Workflows use them for supersession checks:

| Key | Meaning |
|---|---|
| `iteration` | Current clarification iteration number within the lineage. |
| `previous_artifact` | Prior `CLARIFY-*` artifact used as input for this iteration. |
| `previous_status` | Prior artifact status when this iteration was created. |
| `previous_updated` | Prior artifact timestamp or filesystem mtime used for freshness evidence. |
| `previous_sha256` | SHA-256 of the prior artifact when this iteration was created. |
| `previous_develop_artifact` | Prior `DEVELOP-*` artifact summarized into the enriched lineage snapshot, when supplied or unambiguously discovered. |
| `previous_develop_sha256` | SHA-256 of the prior `DEVELOP-*` artifact when it was summarized. |
| `previous_walkthrough_artifact` | Prior walkthrough artifact summarized into the enriched lineage snapshot, when supplied or unambiguously discovered. |
| `previous_commit` | Prior commit/ref summarized into the enriched lineage snapshot, when supplied or unambiguously inferred from the user's loop request. |

Clarification loop lineage is local and temporary. Use explicit, name-versioned artifact and commit/ref references as sufficient evidence when they are supplied or unambiguous. Additional hashes are not required unless a listed `*_sha256` key is already part of a source freshness check. Ambiguous names, missing referenced artifacts, or conflicting evidence are source gaps that must be recorded or escalated.

An artifact named by another `CLARIFY-*` in `previous_artifact` is superseded for default `/answer` selection when the descendant is fresher by `iteration`, then artifact timestamp. Superseded artifacts remain lineage evidence or explicit branch points when the user confirms that intent.

`### Enriched Lineage Snapshot` is the durable summarized context that lets a new clarification iteration stand on its own for `/answer`. It can summarize prior `CLARIFY-*`, matching `DEVELOP-*`, walkthrough, and commit evidence, but it does not replace `source_*` freshness checks, approval records, `/review`, `/optimize`, or `/develop` handoff gates.

### Approval Records

Explicit approval remains the default approval record. A workflow may produce or consume a shared substitute approval record only when this lifecycle, the shared operating model, the producing workflow, and the accepting workflow all allow the same bounded scope.

| Record | Valid Only When | Never Satisfies |
|---|---|---|
| `explicit approval recorded` | The user approves the exact next mutation, scope, and risk. | No record-level exclusions; still satisfy stricter gate-specific requirements. |
| `ApprovalRecord=workflow-tolerated` | A composing workflow explicitly supports producing this record, the route is approval-tolerable, and the pre-mutation record captures producer workflow, accepting gate, bounded scope, Priority Stack decision, source/status/staleness checks when artifacts exist, no unverified assumptions, and planned verification. | Gates whose owning workflow has not opted in, security-sensitive, VCS, destructive, failed-gate, unclear-gate, unresolved-input, unverified-assumption, temp-artifact lifecycle-risk, or final user-approval gates such as setting `status: implemented`. Final completion still requires executed verification evidence. |

### Assumption Tags

| Tag | Meaning | Handoff Rule |
|---|---|---|
| `[ASSUMPTION - unverified]` | Required decision or fact is unresolved | Blocks `draft-self-reviewed`, `clarified`, and `ready-to-develop` |
| `[ASSUMPTION - accepted]` | User or workflow accepted a non-critical uncertainty | Allowed only in `Risk Register` with mitigation and source |

Accepted assumptions never bypass Security, Correctness, testable acceptance criteria, or the required approval record.

### Re-entry
Resume from the last incomplete step identified by the artifact's `status` field and metadata flags.
