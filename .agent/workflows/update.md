---
description: Orchestrate filesystem-driven synchronization of agent instructions, loaders, workflows, and skill index
---

# Update

> **Trigger**: skill/workflow changes or `.agent` portability sync. Scope defaults to `all` only when no retained run exists.
> See [Lifecycle](./_shared/status-lifecycle.md) and [Operating Model](./_shared/workflow-operating-model.md).
> **Estimated context: ~2.3K tokens**

## 1. Mission And State

Run the Startup and Repository Extension gates once. Read the plan, this workflow, shared contracts, and only delegated workflow/skill context.

### Run And Scope Reconciliation

Normalize an explicit invocation scope through the Plan Contract before reading retained state.

1. With no retained plan, start a new run from the explicit scope or `all`.
2. For a non-terminal plan, a missing explicit scope resumes its recorded `Requested Scope`; an exact match resumes the same run. A different explicit scope blocks mutation until the user chooses the active run or supersedes it through `/update-plan`.
3. For a `verified` plan, any explicit scope delegates to `/update-plan` for a fresh run, even when equal to the prior scope. A scope-less invocation stops only when the current verification binding still matches.
4. Before honoring `verified`, recompute and compare Run ID, Requested Scope, effective Scope, Semantic Plan SHA-256, and Output Revision SHA-256 against `update-verify-progress.md`. An output-only mismatch invalidates verification and returns the plan to `done` for review; a run, scope, or semantic-plan mismatch requires a fresh plan.
5. A fresh or superseding plan receives a new lowercase UUID v4 Run ID. Never reuse prior verification progress, output manifests, approval, or pass states across Run IDs.

```text
no plan/outlined/planning -> update-plan
ready                    -> review plan
plan-reviewed            -> exact preview + approval -> update-execute
executing                -> update-execute
done                     -> review changes
reviewed/verifying        -> update-verify
failed                    -> correction preview + approval -> update-execute
correcting                -> update-execute
verified + current binding -> stop
```

| State | Action | Post-condition |
|---|---|---|
| no plan / `outlined` / `planning` | Delegate discovery/planning. | Plan progresses. |
| `ready` | `/review update-plan.md`. | No Critical -> `/update` sets `plan-reviewed`. |
| `plan-reviewed` | Run apply-preview gate below. | Complete current approval. |
| `executing` | Delegate/resume execution. | `done`. |
| `done` | Review Stage 1-5 outputs. | No Critical -> `/update` sets `reviewed`. |
| `reviewed` / `verifying` | Delegate verification. | `verified` or `failed`. |
| `failed` | Build and approve the exact correction proposal; delegate correction. | `correcting` then `done`. |
| `correcting` | Resume correction through `/update-execute`. | `done`. |
| `verified` | Reconcile invocation and verification binding. | Stop only when both are current. |

`/review` is read-only. `/update` owns scope reconciliation, preview/approval artifacts, verification invalidation, and review-recommended plan-header transitions; sub-workflows own their declared lifecycle files.

For `all` in a copied repository, rediscover current instructions, profile, skills, workflows, manifests, CI, docs, and project assets. Treat old facts as drift evidence. Sync derived loaders, workflow routes, profile, `AGENTS.md`, and skill index together; flag project-doc drift for `/documentation`.

Use load order: `Basic -> Overview -> DRN Framework -> Testing -> Frontend -> Custom`.

## 2. Situation Report

Before and after delegation, report Run ID, plan path/status, requested/effective scope, stage counts, timestamp, binding state, action/result, and next step. At a current `verified`, suggest cleanup of update temp artifacts and a compliant commit command; never delete or mutate VCS without request.

## 3. Apply-Preview Gate

Plan review is not apply approval. Before the first execution mutation:

1. Compute the Semantic Plan digest below.
2. Persist `.agent/temp/update-apply-preview.md` with Run ID, requested/effective scope, actions/output paths, semantic-plan digest, baseline manifest/hash, Priority Stack risk decision, and exact expected path state plus raw-byte SHA-256 for any output not represented by a proposed diff.
3. If an exact diff exists, persist raw bytes at `.agent/temp/update-proposed.diff` and reference its digest; otherwise remove a stale proposal and use the preview as proposal.
4. Present the exact human-readable preview/diff, scope, and risk for explicit approval.
5. On confirmation, recompute semantic plan, preview/diff, and baseline digests; abort on mismatch.
6. Record the complete shared approval envelope in `## Apply Approval`: subject digest is the preview digest; preview digest is the proposed-diff digest or preview digest when no diff exists.
7. When any envelope input, semantic plan, baseline manifest/hash, preview, or diff changes, first append the complete current approval envelope with invalidation reason and time to `### Approval History`; then invalidate the current approval. Never edit or remove a history record.

For a `failed` run, the Corrections Required table is the correction action list. Re-run this entire gate with a correction-labeled preview and exact diff, invalidate the prior apply approval, and restrict targets to the current run's scope and declared Stage 1-5 outputs. Scope widening or semantic-plan changes require a fresh run. After current explicit approval, `/update-execute` owns `failed -> correcting -> done`.

### Semantic Plan SHA-256

Project raw `.agent/temp/update-plan.md` bytes from the only `## Discovery Summary` heading through EOF:

1. In a stage line `> Status: <value> | Maps to: <value>`, preserve `skipped`; replace `pending`, `executing`, `done`, `blocked`, or `fail` with `<progress>`; reject other/malformed values.
2. Within each `### Actions`, normalize leading checked markers to `- [ ] ` until the next heading.
3. Preserve `### Requires Approval` and every other byte, including line endings.
4. Reject missing/duplicate delimiters.

The lowercase SHA-256 of this projection is stable across execution progress but changes with discovery, actions, paths, risks, required approvals, or behavior.

## 4. Review Scope

For a `done` plan, review the exact repository-relative paths declared under Stage 1-5 `### Outputs`; include in-scope shared fragments, `AGENTS.md` when Stage 3 ran, and the skill index when Stage 5 ran. Exclude Stage 6, which only flags drift.

## 5. Plan Contract

Location: `.agent/temp/update-plan.md`. Required content:

- Header with generated time, Run ID, status, Requested Scope, effective Scope, stages, repository, baseline HEAD, Baseline Inputs Hash, and Baseline Inputs Manifest.
- Custom groups/routes.
- `## Apply Approval`.
- Discovery Summary: skills, projects, assets, drift, documentation drift.
- Stages 1-6: group loaders; all-skills loader; AGENTS/profile; non-project references; skill index; project-doc flags.

| Scope | Stages / discovery |
|---|---|
| `all` / omitted | 1-6 / full filesystem |
| `<group>` | 1, 2, 5 / group skills |
| `<skill-dir>` | 1, 2, 5 / selected skill |
| `skills` | 1, 2, 5 / all skills |
| `agents` | 3 / projects and assets |
| `projects` | 3, 4, 6 / projects and assets |
| `infra` | 4 / infrastructure |
| `files: <paths>` | Path-derived; preserve exact list |
| `stage-N` | Named stage |
| freeform | Planner resolves |

Ask before widening scope. Resume the first non-terminal stage and pause at unresolved `Requires Approval`.

Minimum template:

```markdown
# Update Plan
> Generated: <timestamp> | Run ID: <lowercase UUID v4> | Status: <status>
> Requested Scope: <normalized invocation scope> | Scope: <effective scope> | Resolved Stages: <stages>
> Repo: <path> | Baseline HEAD: <sha> | Baseline Inputs Hash: <sha256 or N/A> | Baseline Inputs Manifest: .agent/temp/update-baseline-inputs.manifest or N/A
<!-- Include the next blockquote only when Baseline Inputs Hash is N/A; otherwise omit it. -->
> Baseline Inputs Hash Justification: no-material-input-files
> Custom Groups: <prefix -> workflow>
> Custom Workflows: <route -> workflow>

## Apply Approval
### Current Approval
> approval_required: true
> approval_record: pending
> approval_scope:
> approval_subject: .agent/temp/update-apply-preview.md
> approval_subject_sha256:
> approval_preview_sha256:
> approval_producer:
> approval_recorded_at:
> approval_risk_decision:
> approval_envelope_sha256:

### Approval History
<!-- Omit approval history records until an actual invalidation occurs. When invalidating, append the current approval state and its complete envelope before every Run ID or approval-state change; never edit or remove prior records:
#### Superseded Approval: <invalidated-at>
> invalidation_reason:
> run_id:
> approval_required:
> approval_record:
> approval_scope:
> approval_subject:
> approval_subject_sha256:
> approval_preview_sha256:
> approval_producer:
> approval_recorded_at:
> approval_risk_decision:
> approval_envelope_sha256:
-->

## Discovery Summary
### Skills Manifest
### Projects Manifest
### Non-Project Assets
### Drift Report
### Documentation Drift

## Stage <N>: <Title>
> Status: <pending|skipped|executing|done|blocked|fail> | Maps to: §<refs>
### Actions
- [ ] <action>
### Outputs
- <exact repository-relative path or N/A>
### Requires Approval
- [ ] <approval item>
```

Every mutating Stage 1-5 action must map to at least one exact `### Outputs` path before plan review. Deduplicate output paths bytewise; use `N/A` only for a non-mutating stage. Unlisted output mutation makes the plan stale.

Compute and persist the canonical baseline manifest per [baseline-inputs-hash-spec.md](./_shared/baseline-inputs-hash-spec.md). `N/A` requires the exact header `Baseline Inputs Hash Justification: no-material-input-files`, `Baseline Inputs Manifest: N/A`, and no `.agent/temp/update-baseline-inputs.manifest`; omit the justification otherwise.

## 6. Guarantees

Stateful, resumable, scope-aware, revision-bound, Git-tracked, and reversible. Never treat plan review as apply approval. Suggest VCS actions only unless explicitly requested.
