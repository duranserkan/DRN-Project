---
description: Orchestrate filesystem-driven synchronization of agent instructions, loaders, workflows, and skill index
---

# Update

> **Trigger**: skill/workflow changes or `.agent` portability sync. Scope defaults to `all` only when no retained run exists.
> See [Lifecycle](./_shared/status-lifecycle.md) and [Operating Model](./_shared/workflow-operating-model.md).

## 1. Mission And State

Run the Startup and Repository Extension gates once. Read the plan, this workflow, shared contracts, and only delegated workflow/skill context.

### Run And Scope Reconciliation

Normalize an explicit invocation scope through the Plan Contract before reading retained state.

1. With no retained plan, start a new run from the explicit scope or `all`.
2. For a non-terminal plan, use its recorded `Requested Scope` when scope is omitted. A different scope blocks mutation until the user chooses the active run or approves supersession. Before same-scope resume, check [Replanning And Supersession](#replanning-and-supersession). Resume only when no replacement is required.
3. For a `verified` plan, explicit replanning or any explicit scope selects a fresh run through [Replanning And Supersession](#replanning-and-supersession). Retain recorded `Requested Scope` when omitted. Only ordinary scope-less `/update` may stop when the verification binding still matches.
4. Before honoring `verified`, revalidate Baseline HEAD and material non-output inputs using the [baseline contract](./_shared/baseline-inputs-hash-spec.md). Recompute and compare their binding, Run ID, Requested Scope, effective Scope, Semantic Plan SHA-256, and Output Revision SHA-256 against `update-verify-progress.md`. An output-only mismatch returns the plan to `done` for review. Any other mismatch requires a fresh plan. Missing or uncertain baseline evidence blocks reuse.
5. A fresh or superseding plan receives a new lowercase UUID v4 Run ID. Never reuse prior verification progress, output manifests, approval, or pass states across Run IDs.

### Replanning And Supersession

A confirmed `Baseline HEAD` or non-output baseline mismatch replaces the retained run, including `verified`. Explicit replanning selects a new run even when scope is unchanged, including from `verified`. Direct `/update-plan` requests replanning after `outlined`/`planning`. At those two states it normally continues detailing. Ordinary `/update` resumes a current non-terminal run. A failed or unavailable check is not proof of drift. Stop on uncertain evidence.

Before replacing retained control artifacts, `/update` appends the current approval state and complete envelope, when present, with reason/time to `### Approval History` and invalidates current approval. Preserve the prior plan, history, preview/diff, baseline, output manifests, and verification/correction progress in a collision-safe `.agent/temp/update-history/<old-run-id>/` snapshot. Verify preserved bytes before replacement. On interruption, verify and reuse matching preserved evidence. Never overwrite history or continue with incomplete preservation.

Delegate the new run to `/update-plan`. Keep source files, partial outputs, index, and refs untouched during replanning. Record partial work and unresolved differences. Discover current state and bind new exact output preimages. Unexplained changes require user resolution before apply. Carry approval history and the snapshot reference into the replacement plan as evidence only. Require fresh plan review and explicit exact-preview approval. Supersession never authorizes remaining source edits or imports old completion/pass states.

| State | Action | Post-condition |
|---|---|---|
| no plan / `outlined` / `planning` | Delegate discovery/planning. | Plan progresses. |
| `ready` | `/review update-plan.md`. | No unresolved Critical or Major -> `/update` sets `plan-reviewed`. |
| `plan-reviewed` | Run apply-preview gate below. | Complete current approval. |
| `executing` | Delegate/resume execution. | `done`. |
| `done` | Review Stage 1-5 outputs. | No unresolved Critical or Major -> `/update` sets `reviewed`. |
| `reviewed` / `verifying` | Delegate verification. | `verified` or `failed`. |
| `failed` | Build and approve the exact correction proposal. Delegate correction. | `correcting` then `done`. |
| `correcting` | Resume correction through `/update-execute`. | `done`. |
| `verified` | Reconcile invocation and verification binding. | Stop only when both are current. |

`/review` is read-only. `/update` owns scope reconciliation, preview/approval artifacts, verification invalidation, and review-recommended plan-header transitions. Sub-workflows own their declared lifecycle files.

For `all` in a copied repository, rediscover current instructions, profile, skills, workflows, manifests, CI, docs, and project assets. Treat old facts as drift evidence. Sync derived loaders, workflow routes, profile, `AGENTS.md`, and skill index together. Flag project-doc drift for `/documentation`.

Use load order: `Basic -> Overview -> Framework -> Testing -> Frontend -> Custom`. Resolve framework groups from the profile.

## 2. Situation Report

Before and after delegation, report Run ID, plan path/status, requested/effective scope, stage counts, timestamp, binding state, action/result, and next step. At a current `verified`, suggest cleanup of update temp artifacts and a compliant commit command. Never delete or mutate VCS without request.

## 3. Apply-Preview Gate

Plan review is not apply approval. Before the first execution mutation:

1. Compute the Semantic Plan digest below.
2. Persist `.agent/temp/update-apply-preview.md` with Run ID, requested/effective scope, actions/output paths, semantic-plan digest, baseline manifest/hash, Priority Stack risk decision, and every output's exact preimage and expected postimage presence, canonical mode, and raw-byte SHA-256. The preview's raw-byte SHA-256 binds the complete tuple list.
3. If an exact diff exists, persist raw bytes at `.agent/temp/update-proposed.diff` and reference its digest. Otherwise remove a stale proposal and use the preview as proposal. The diff is human-readable change evidence and never replaces an output preimage.
4. Present the exact human-readable preview/diff, scope, and risk for explicit approval.
5. On confirmation, recompute semantic plan, preview/diff, baseline digests, and every current output preimage. Abort on mismatch.
6. Record the complete shared approval envelope in `## Apply Approval`. Subject digest is the preview digest. Preview digest is the proposed-diff digest or preview digest when no diff exists.
7. When any envelope input, semantic plan, baseline manifest/hash, output preimage/postimage tuple, preview, or diff changes, first append the complete current approval envelope with invalidation reason and time to `### Approval History`. Then invalidate the current approval. Never edit or remove a history record.

For a `failed` run, the Corrections Required table is the correction action list. Re-run this entire gate with a correction-labeled preview and exact diff, invalidate the prior apply approval, and restrict targets to the current run's scope and declared Stage 1-5 outputs. Scope widening or semantic-plan changes require a fresh run. After current explicit approval, `/update-execute` owns `failed -> correcting -> done`.

### Semantic Plan SHA-256

Project raw `.agent/temp/update-plan.md` bytes from the only `## Discovery Summary` heading through EOF:

1. In a stage line `> Status: <value> | Maps to: <value>`, preserve `skipped`. Replace `pending`, `executing`, `done`, `blocked`, or `fail` with `<progress>`. Reject other/malformed values.
2. Within each `### Actions`, normalize leading checked markers to `- [ ] ` until the next heading.
3. Preserve `### Requires Approval` and every other byte, including line endings.
4. Reject missing/duplicate delimiters.

The lowercase SHA-256 of this projection is stable across execution progress but changes with discovery, actions, paths, risks, required approvals, or behavior.

## 4. Review Scope

For a `done` plan, review the exact repository-relative paths declared under Stage 1-5 `### Outputs`. Include in-scope shared fragments, `AGENTS.md` when Stage 3 ran, and the skill index when Stage 5 ran. Exclude Stage 6, which only flags drift.

## 5. Plan Contract

Location: `.agent/temp/update-plan.md`. Required content:

- Header with generated time, Run ID, status, Requested Scope, effective Scope, stages, repository, baseline HEAD, Baseline Inputs Hash, and Baseline Inputs Manifest.
- Custom groups/routes.
- `## Apply Approval`.
- Discovery Summary: skills, projects, assets, drift, documentation drift.
- Stages 1-6: group loaders, all-skills loader, AGENTS/profile, non-project references, skill index, project-doc flags.

| Scope | Stages / discovery |
|---|---|
| `all` / omitted | 1-6 / full filesystem |
| `<group>` | 1, 2, 5 / group skills |
| `<skill-dir>` | 1, 2, 5 / selected skill |
| `skills` | 1, 2, 5 / all skills |
| `agents` | 3 / projects and assets |
| `projects` | 3, 4, 6 / projects and assets |
| `infra` | 4 / infrastructure |
| `files: <paths>` | Path-derived. Preserve exact list |
| `stage-N` | Named stage |
| freeform | Planner resolves |

Ask before widening scope. Resume the first non-terminal stage and pause at unresolved `Requires Approval`.

Minimum template:

```markdown
# Update Plan
> Generated: <timestamp> | Run ID: <lowercase UUID v4> | Status: <status>
> Requested Scope: <normalized invocation scope> | Scope: <effective scope> | Resolved Stages: <stages>
> Repo: <path> | Baseline HEAD: <sha> | Baseline Inputs Hash: <sha256 or N/A> | Baseline Inputs Manifest: .agent/temp/update-baseline-inputs.manifest or N/A
<!-- Include the next blockquote only when Baseline Inputs Hash is N/A. Otherwise omit it. -->
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
<!-- Omit approval history records until an actual invalidation occurs. When invalidating, append the current approval state and its complete envelope before every Run ID or approval-state change. Never edit or remove prior records:
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

Every mutating Stage 1-5 action must map to at least one exact `### Outputs` path before plan review. Deduplicate output paths bytewise. Use `N/A` only for a non-mutating stage. Unlisted output mutation makes the plan stale.

Compute and persist the baseline checksum manifest per [baseline-inputs-hash-spec.md](./_shared/baseline-inputs-hash-spec.md). `N/A` requires the exact header `Baseline Inputs Hash Justification: no-material-input-files`, `Baseline Inputs Manifest: N/A`, and no baseline artifacts. Omit the justification otherwise.

## 6. Guarantees

Stateful, resumable, scope-aware, revision-bound, Git-tracked, and reversible. Never treat plan review as apply approval. Suggest VCS actions only unless explicitly requested.
