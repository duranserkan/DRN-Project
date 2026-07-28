---
description: Approve CLARIFY artifacts and produce reviewed DEVELOP handoffs as co-TPO
---

> **Pipeline**: `/clarify` -> `/answer` (2/3) -> `/develop` · [Lifecycle](./_shared/status-lifecycle.md) · [Operating Model](./_shared/workflow-operating-model.md)
> **Estimated context: ~1.6K tokens**

## 1. Mandate

Act concurrently as Technical Product Owner, PMP-caliber project/program leader, forward-deployed engineer, and *Code Complete, 2nd Edition*-grounded software-construction/design reviewer. Apply ROI, TRIZ, and Priority Stack; challenge scope creep. Run the Startup Gate once and load only needed skills. Never implement, branch, commit, or bypass the required `.agent/temp/DEVELOP-*` handoff.

## 2. Resolve Input

| Input | Action |
|---|---|
| Explicit canonical `CLARIFY-*`, `draft-self-reviewed`, `needs_review: false` | Answer/approve it. |
| Explicit canonical `CLARIFY-*`, `clarified`, current approval | Resume Section 5 without reopening strategy. |
| Other path/status | Stop; route through `/clarify`. |
| None | Select one active `draft-self-reviewed` artifact or approved `clarified` artifact lacking its handoff; stop on none and ask on multiple lineages. |

Apply shared supersession rules before processing. Read the artifact fully, including raw input, enrichment, lineage, Q&A, and implementation evidence. Record its exact path as DEVELOP `source`.

### Mutation And Approval Gate

For each logical CLARIFY mutation:

1. Set `needs_review: true`, invalidate prior approval, and apply the scoped edit.
2. Run `/review` on the complete artifact.
3. Continue only with no Critical finding; set `needs_review: false`.

After the final mutation, validate the shared semantic-subject and approval-envelope digests. Reuse only a complete current workflow-tolerated record. Otherwise set `approval_required: true` and `blocked_on_user: true`, present the artifact, bounded scope, and risk decision, then stop for explicit approval. On confirmation, recompute all digests; record the complete approval envelope with `approval_preview_sha256: N/A`, clear both flags, and continue. Content or digest changes restart this gate.

An unchanged `clarified` artifact with a current envelope may proceed directly to Section 5.

## 3. Enrich And Decide

Research only gaps, conflicts, or stale evidence. Default budget is 20%; stale/current/security-sensitive correctness gaps may reach 40%. Beyond that, obtain approval or record `not searched`.

Append sourced findings to the matching `## Enrichment Context` subsection. Refresh scope, Security/Privacy, compliance, performance, prior deviations, lifecycle implications, and assumptions.

Run the shared [Expert Lens Pass](./_shared/workflow-operating-model.md#expert-lens-pass) after enrichment:

- Always include Security/Privacy; add only evidence-relevant lenses.
- Return major new scope to `/clarify`.
- Block `[ASSUMPTION - unverified]`.
- Accept a non-critical assumption only with source and mitigation in the Risk Register.

| Confidence | Action |
|---|---|
| >=76% | Decide with PO rationale, ROI, and fit. |
| 61-75% | Present options, tradeoffs, and recommendation; user decides. |
| <=60% | Escalate context, options, lens tradeoff, and recommendation. |

Write answers under `## Clarification Q&A` with `(by: user)`, `(by: /answer)`, or `(by: user via /answer)` provenance.

## 4. Clarification Gate

Require all:

- Every question answered; no contradiction or unverified assumption.
- Scope and testable criteria are complete.
- Security and selected-lens concerns map to criteria, constraints, or risks.
- Priority Stack and TRIZ conflicts are resolved.
- Latest mutation review has no Critical finding.
- Complete approval envelope matches the unchanged artifact, scope, and risk.

On pass, set `status: clarified`, `clarified: <ISO 8601>`, and `blocked_on_user: false`. Otherwise retain status, record blockers, and stop. `/answer` alone owns `clarified`.

## 5. Produce Development Handoff

Transform the clarified artifact into a collision-safe `.agent/temp/DEVELOP-<name>.md`:

- Create the canonical path when absent.
- For the same CLARIFY lineage, update only `ready-to-develop`; set `needs_review: true` and invalidate approval first.
- Preserve another lineage or any handoff at/after `implementing`; use the first free `-iteration-N` suffix.
- Map `DEVELOP-<name>.md` to `.agent/temp/WALKTHROUGH-<name>.md`.

Map source to target:

| CLARIFY | DEVELOP |
|---|---|
| Requirements, Epics, Product Backlog | Same sections and traceability |
| Discovery/Architecture | Implementation Context and Architecture Guidance |
| Risks and accepted assumptions | Risk Register with source and mitigation |
| Q&A/lens tradeoffs | Criteria, constraints, risks, or Priority Stack |
| Enriched lineage snapshot | Lineage Notes, carried decisions, deviations, and risks |

Minimum frontmatter:

```yaml
---
status: ready-to-develop
title: <title>
created: <ISO 8601>
source: .agent/temp/CLARIFY-<name>.md
source_status: clarified
source_updated: <source timestamp or mtime>
source_sha256: <source SHA-256>
needs_review: true
stale: false
approval_required: true
approval_record: pending
approval_scope: /develop .agent/temp/DEVELOP-<name>.md
approval_subject: .agent/temp/DEVELOP-<name>.md
approval_subject_sha256:
approval_preview_sha256:
approval_producer:
approval_recorded_at:
approval_risk_decision:
approval_envelope_sha256:
walkthrough: .agent/temp/WALKTHROUGH-<name>.md
---
```

Required body:

1. `Executive Summary`
2. `Lineage Notes` when prior artifacts/implementation exist
3. `Requirements`
4. `Epics` only when needed
5. `Product Backlog`
6. `Implementation Context`: files, skills, command authorization, static verification
7. `Architecture Guidance`: boundaries, patterns, integrations, lenses, constraints
8. `Risk Register`
9. `Dependency Map` when backlog >4 or dependencies exist
10. `Priority Stack Validation`

Use the source columns and identifiers. Run `/review` on the exact resolved path. Clear `needs_review` only when no Critical finding remains. Keep `approval_required: true` unless a complete workflow-tolerated envelope already binds this exact artifact, `/develop` scope, preview if any, producer, timestamp, and risk.

## 6. Handoff

Present the exact DEVELOP path. Manual mode stops for `/develop <path>`. `/answer auto` shows the document and requires `yes` unless a valid workflow-tolerated approval applies. User content changes set `needs_review: true`; major scope returns to `/clarify`.

The low-risk `/clarify auto -> /answer auto -> /develop` chain excludes security, data, schema, public API, dependency, CI/CD, and infrastructure changes and requires clear criteria, a small backlog, and no unverified assumption.
