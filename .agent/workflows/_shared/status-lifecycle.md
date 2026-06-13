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
| `implemented` | `DEVELOP-*` | User approves final report (§7.2) | `/develop` |
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
| `approval_required: true` | The next step mutates state, VCS, or risk-bearing scope | Explicit approval is recorded |

### Assumption Tags

| Tag | Meaning | Handoff Rule |
|---|---|---|
| `[ASSUMPTION - unverified]` | Required decision or fact is unresolved | Blocks `draft-self-reviewed`, `clarified`, and `ready-to-develop` |
| `[ASSUMPTION - accepted]` | User or workflow accepted a non-critical uncertainty | Allowed only in `Risk Register` with mitigation and source |

Accepted assumptions never bypass Security, Correctness, testable acceptance criteria, or user approval.

### Re-entry
Resume from the last incomplete step identified by the artifact's `status` field and metadata flags.
