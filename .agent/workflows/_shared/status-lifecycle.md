---
description: Shared status lifecycle for agent workflow artifacts
---

> **Estimated context: ~1.5K tokens**
> See also: [Workflow Operating Model](./workflow-operating-model.md)

## Status Lifecycle

```text
CLARIFY-* : draft -> clarifying -> draft-self-reviewed -> clarified
DEVELOP-* : ready-to-develop -> implemented
UPDATE    : outlined -> planning -> ready -> plan-reviewed -> executing -> done -> reviewed -> verifying -> verified
UPDATE    : failed -> verifying -> verified | failed
```

### Status Transitions

| Status | Artifact | Advance When | Owner |
|---|---|---|---|
| `draft` | `CLARIFY-*` | `/clarify` creates the document | `/clarify` |
| `clarifying` | `CLARIFY-*` | `/clarify` starts question round 1 | `/clarify` |
| `draft-self-reviewed` | `CLARIFY-*` | `/clarify` gates and self-review pass | `/clarify` |
| `clarified` | `CLARIFY-*` | `/answer` approval criteria pass | `/answer` |
| `ready-to-develop` | `DEVELOP-*` | `/answer` writes the development handoff | `/answer` |
| `implemented` | `DEVELOP-*` | User approves the `/develop` final report | `/develop` |
| `outlined` | `update-plan.md` | Initial plan shell exists | `/update-plan` |
| `planning` | `update-plan.md` | Discovery or detailing runs | `/update-plan` |
| `ready` | `update-plan.md` | Stages are resolved and awaiting review | `/update-plan` |
| `plan-reviewed` | `update-plan.md` | `/review` reports `transition_allowed: plan-reviewed` | `/update` |
| `executing` | `update-plan.md` | First execution stage starts | `/update-execute` |
| `done` | `update-plan.md` | All in-scope execution stages complete | `/update-execute` |
| `reviewed` | `update-plan.md` | `/review` reports `transition_allowed: reviewed` | `/update` |
| `verifying` | `update-plan.md`, `update-verify-progress.md` | Verification starts or resumes | `/update-verify` |
| `verified` | `update-plan.md`, `update-verify-progress.md` | Verification verdict passes | `/update-verify` |
| `failed` | `update-plan.md`, `update-verify-progress.md` | Verification verdict fails | `/update-verify` |

### Metadata Flags

Use lowercase YAML flags; never replace `status`.

| Flag | Meaning | Clear When |
|---|---|---|
| `blocked_on_user: true` | Human decision blocks status advance | Decision is recorded |
| `needs_review: true` | Artifact changed after last review | `/review` passes with no critical findings |
| `stale: true` | Source changed after artifact production | Artifact is regenerated or revalidated |
| `approval_required: true` | Next step mutates source/state, VCS, or risk-bearing scope without current approval | `approval_record` and `approval_scope` cover the exact next mutation |

When clearing `approval_required`, keep matching `approval_record` and `approval_scope`. `approval_required: false` without a current matching record remains an unresolved approval gate.

### Lineage Metadata

`CLARIFY-*` artifacts may use these keys when a new clarification loop starts from an earlier artifact. Use them for supersession.

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

- **Evidence**: Accept explicit name-versioned artifacts and commits/refs when supplied or unambiguous. Require hashes only when a listed `*_sha256` key drives freshness. Record or escalate source gaps.
- **Supersession**: Same-lineage descendants supersede `previous_artifact` by higher `iteration`, then newer timestamp. Treat superseded artifacts as evidence or branch points only with user confirmation.
- **Snapshot Boundary**: `### Enriched Lineage Snapshot` lets a new clarification iteration stand alone for `/answer`. It may summarize prior `CLARIFY-*`, matching `DEVELOP-*`, walkthrough, and commit evidence. It never replaces `source_*` freshness checks, approval records, `/review`, `/optimize`, or `/develop` gates.

### Approval Records

Use explicit approval by default. Use a substitute only when this lifecycle, the operating model, the producer, and the accepting workflow all allow the same bounded scope.

| Record | Valid Only When | Never Satisfies |
|---|---|---|
| `explicit approval recorded` | User approves the exact next mutation, scope, and risk | No record-level exclusions; still satisfy stricter gate rules |
| `ApprovalRecord=workflow-tolerated` | Workflow opts in; route is approval-tolerable; pre-mutation record captures producer, gate, scope, Priority Stack decision, source/status/staleness checks when artifacts exist, no unverified assumptions, and planned verification | Non-opted-in, security-sensitive, VCS, destructive, failed/unclear-gate, unresolved-input, unverified-assumption, temp-artifact lifecycle-risk, or final user-approval gates, including `status: implemented`. Final completion still needs verification evidence |

### Assumption Tags

| Tag | Meaning | Handoff Rule |
|---|---|---|
| `[ASSUMPTION - unverified]` | Required decision or fact is unresolved | Blocks `draft-self-reviewed`, `clarified`, and `ready-to-develop` |
| `[ASSUMPTION - accepted]` | User or workflow accepted a non-critical uncertainty | Allowed only in `Risk Register` with mitigation and source |

Accepted assumptions never bypass Security, Correctness, testable acceptance criteria, or the required approval record.

### Re-entry

Resume from the last incomplete step named by `status` and metadata flags.
