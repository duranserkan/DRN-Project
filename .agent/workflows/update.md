---
description: Orchestrate filesystem-driven synchronization of agent instructions, loaders, workflows, and skill index
---

> **Trigger**: skill/workflow changes or `.agent` portability sync. Scope defaults to `all`.
> See [Lifecycle](./_shared/status-lifecycle.md) and [Operating Model](./_shared/workflow-operating-model.md).
> **Estimated context: ~1.8K tokens**

## 1. Mission And State

Run the Startup and Repository Extension gates once. Read the plan, this workflow, shared contracts, and only delegated workflow/skill context.

```text
no plan/outlined/planning -> update-plan
ready                    -> review plan
plan-reviewed            -> exact preview + approval -> update-execute
executing                -> update-execute
done                     -> review changes
reviewed/verifying/failed -> update-verify
verified                 -> stop
```

| State | Action | Post-condition |
|---|---|---|
| no plan / `outlined` / `planning` | Delegate discovery/planning. | Plan progresses. |
| `ready` | `/review update-plan.md`. | No Critical -> `/update` sets `plan-reviewed`. |
| `plan-reviewed` | Run apply-preview gate below. | Complete current approval. |
| `executing` | Delegate/resume execution. | `done`. |
| `done` | Review Stage 1-5 outputs. | No Critical -> `/update` sets `reviewed`. |
| `reviewed` / `verifying` / `failed` | Delegate verification. | `verified` or `failed`. |
| `verified` | Stop; suggest cleanup/commit only. | No mutation. |

`/review` is read-only. `/update` may apply only its returned plan-header transition; sub-workflows own their lifecycle files.

For `all` in a copied repository, rediscover current instructions, profile, skills, workflows, manifests, CI, docs, and project assets. Treat old facts as drift evidence. Sync derived loaders, workflow routes, profile, `AGENTS.md`, and skill index together; flag project-doc drift for `/documentation`.

Use load order: `Basic -> Overview -> DRN Framework -> Testing -> Frontend -> Custom`.

## 2. Situation Report

Before and after delegation, report plan path/status, scope, stage counts, timestamp, action/result, and next step. At `verified`, suggest cleanup of update temp artifacts and a compliant commit command; never delete or mutate VCS without request.

## 3. Apply-Preview Gate

Plan review is not apply approval. Before the first execution mutation:

1. Compute the Semantic Plan digest below.
2. Persist `.agent/temp/update-apply-preview.md` with scope, actions/output paths, semantic-plan digest, baseline manifest/hash, and Priority Stack risk decision.
3. If an exact diff exists, persist raw bytes at `.agent/temp/update-proposed.diff` and reference its digest; otherwise remove a stale proposal and use the preview as proposal.
4. Present the exact human-readable preview/diff, scope, and risk for explicit approval.
5. On confirmation, recompute semantic plan, preview/diff, and baseline digests; abort on mismatch.
6. Record the complete shared approval envelope in `## Apply Approval`: subject digest is the preview digest; preview digest is the proposed-diff digest or preview digest when no diff exists.
7. Invalidate approval when any envelope input, semantic plan, baseline manifest/hash, preview, or diff changes.

### Semantic Plan SHA-256

Project raw `.agent/temp/update-plan.md` bytes from the only `## Discovery Summary` heading through EOF:

1. In a stage line `> Status: <value> | Maps to: <value>`, preserve `skipped`; replace `pending`, `executing`, `done`, `blocked`, or `fail` with `<progress>`; reject other/malformed values.
2. Within each `### Actions`, normalize leading checked markers to `- [ ] ` until the next heading.
3. Preserve `### Requires Approval` and every other byte, including line endings.
4. Reject missing/duplicate delimiters.

The lowercase SHA-256 of this projection is stable across execution progress but changes with discovery, actions, paths, risks, required approvals, or behavior.

## 4. Review Scope

For a `done` plan, review Stage 1-5 action files and in-scope shared fragments; include `AGENTS.md` when Stage 3 ran and the skill index when Stage 5 ran. Exclude Stage 6, which only flags drift.

## 5. Plan Contract

Location: `.agent/temp/update-plan.md`. Required content:

- Header with generated time, status, scope, stages, repository, baseline HEAD, Baseline Inputs Hash, and Baseline Inputs Manifest.
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
> Generated: <timestamp> | Status: <status> | Scope: <scope> | Resolved Stages: <stages>
> Repo: <path> | Baseline HEAD: <sha> | Baseline Inputs Hash: <sha256 or N/A> | Baseline Inputs Manifest: .agent/temp/update-baseline-inputs.manifest or N/A
> Custom Groups: <prefix -> workflow>
> Custom Workflows: <route -> workflow>

## Apply Approval
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
### Requires Approval
- [ ] <approval item>
```

Compute and persist the canonical baseline manifest per [baseline-inputs-hash-spec.md](./_shared/baseline-inputs-hash-spec.md). `N/A` requires `Baseline Inputs Manifest: N/A`, no retained manifest, and exact header `Baseline Inputs Hash Justification: no-material-input-files`; omit that justification otherwise.

## 6. Guarantees

Stateful, resumable, scope-aware, Git-tracked, and reversible. Never treat plan review as apply approval. Suggest VCS actions only unless explicitly requested.
