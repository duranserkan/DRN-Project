---
description: Answer clarification questions as co-TPO, approve CLARIFY artifacts, and produce DEVELOP handoffs using DiSCOS, AGENTS.md, and repository skills
---

> **Pipeline**: `/clarify` -> `/answer` (2/3) -> `/develop` · [Status Lifecycle](./_shared/status-lifecycle.md) · [Operating Model](./_shared/workflow-operating-model.md)
> **Estimated context: ~1.2K tokens**

---

## 1. Role & Mandate
**Technical Product Owner (co-TPO)**: The user owns domain authority; you supply analytical rigor, ROI framing, and Priority Stack discipline. Challenge scope creep and propose leaner alternatives.

Apply the shared Startup Gate before work: read `AGENTS.md`, `.agent/rules/DiSCOS.md` when present, `.agent/repository-profile.md` when present, this workflow, and only needed skills.

- **No Artifact Skipping**: Generate or update `.agent/temp/DEVELOP-[task-slug].md` before `/develop`. Never transition directly to code edits or implementation from `/answer`.

---

## 2. Resolve Input
Identify the active `CLARIFY-*` artifact:
- **Explicit path** (e.g., `/answer .agent/temp/CLARIFY-x.md`): Use that file.
- **With user insights** (e.g., `/answer .agent/temp/CLARIFY-x.md "We need OAuth2"`): Use file + integrate insights.
- **No arguments**: Scan `.agent/temp/` for `CLARIFY-*.md`. If none, stop: *"No clarification document found. Run `/clarify [task]` first."* If one, use it. If multiple, apply §2a and use the single latest non-superseded artifact only when unambiguous; otherwise ask.

### 2a. Supersession Gate

Before answering or transforming a `CLARIFY-*` artifact, inspect discovered lineage metadata:
- Treat artifact `A` as superseded when a same-lineage descendant names `A` in `previous_artifact` and is fresher by `iteration`, then artifact timestamp.
- If explicit input is superseded, stop and redirect to the latest lineage artifact. Continue from the older artifact only when the user explicitly confirms a branch from that state.
- If no-argument resolution leaves multiple latest artifacts or lineages, ask which one to use.
- Record the resolved artifact path in the `DEVELOP-*` `source` field. Older artifacts remain lineage evidence, not the default `/answer` target.

Read the document fully: raw input, enrichment, `### Enriched Lineage Snapshot`, open questions, iteration history, and any previous implementation evidence summarized there.

For local temporary lineage, treat explicit name-versioned artifact and commit/ref references in the snapshot as sufficient evidence unless they are missing, ambiguous, stale under an existing `source_*` freshness gate, or contradicted by the summary. Do targeted research only for those gaps or conflicts.

---

## 3. Enrich Context
Review `## Enrichment Context`, including `### Enriched Lineage Snapshot` when present. If open-question, lineage, or previous-implementation gaps exist, do targeted research (max 20%; do not repeat `/clarify`) and append findings to the matching subheading with `(by: /answer)` tags:
```markdown
### Codebase Findings
- (by: /answer) [Additional finding relevant to open questions]
```

Refresh the risk/scope check before answering: boundaries, Security/Privacy, compliance/data/performance/design, lifecycle/approval, stale assumptions, source gaps, previous `DEVELOP-*` stale/needs-review flags, and prior commit or walkthrough deviations. Cite evidence or tag assumptions; do not invent facts. If a prior implementation snapshot conflicts with the current clarification scope, resolve it through answers, requirements, risks, or a human decision before approval.

---

## 4. Answer Questions
Read `## Clarification Q&A`, draft candidate answers privately, then run the shared [Expert Lens Pass](./_shared/workflow-operating-model.md#expert-lens-pass) after targeted enrichment and the updated risk/scope check. Select lenses from evidence and challenge answers for ROI, scope creep, Security/Privacy, compliance, UX/accessibility, database/performance, implementation risk, and verification fit.

| Confidence | Action |
|---|---|
| ≥ 76% | Give a decisive answer with confidence, value, ROI, and fit. |
| 61-75% | Present alternatives, tradeoffs, and a recommendation when evidence supports one; leave unresolved choices to the human TPO/user. |
| ≤ 60% | Escalate with this template: |

  ```markdown
  > **Decision needed** (by: human TPO/user):
  > **Context**: [why this matters, 1–2 sentences]
  > **Options**: A) … B) … C) …
  > **Expert-lens tradeoff**: [compact summary when useful]
  > **Recommendation**: Option A because …
  ```

Integrate user insights. Use **TRIZ** for competing constraints, then **Priority Stack**. Return to `/clarify` for major new scope or unresolved critical assumptions. For direct user answers, validate consistency and suggest improvements without overriding. The co-TPO may recommend, but must not approve its own unresolved decision. Retag accepted non-critical assumptions as `[ASSUMPTION - accepted]` only with mitigation and source, then carry them into `Risk Register`. `[ASSUMPTION - unverified]` is a hard block.

Write directly into the active `.agent/temp/CLARIFY-*.md` document under `## Clarification Q&A`:
```markdown
**Answers** _(by: /answer)_:
1. [optional lens label] [answer with PO rationale]
```
Tag source: `(by: user)`, `(by: /answer)`, or `(by: user via /answer)`. If an answer is given by an expert lens, label it with that lens; otherwise leave it unlabeled.

---

## 5. Review Answers
Self-review answers:
- [ ] **Consistent**: No contradictions.
- [ ] **Complete**: Every question answered.
- [ ] **Actionable**: Developer can act without more clarification.
- [ ] **Aligned**: Priority Stack applied; TRIZ used for conflicts.
- [ ] **Decisive**: Uses "will" and "must", not hedging.
- [ ] **Traceable**: Links to the original question and user need.
- [ ] **Lens-challenged**: Lens concerns are resolved, escalated, or carried into risks, constraints, acceptance criteria, or implementation guidance.

---

## 6. Approval Decision
`/answer` is the sole owner of `status: clarified`.

| Criterion | Check |
|---|---|
| **All questions answered** | No open questions remain |
| **No critical assumptions** | All `[ASSUMPTION - unverified]` resolved; accepted assumptions retagged `[ASSUMPTION - accepted]` with mitigation |
| **Scope is clear** | In/out-of-scope unambiguous |
| **Acceptance criteria exist** | Every requirement has testable criteria |
| **Security addressed** | Security implications captured where relevant |
| **Lens concerns handled** | Security/compliance/performance/design implications resolved, escalated, or carried into constraints and `Risk Register` |

- **All criteria pass AND approval is recorded**: Set `status: clarified`, `clarified: [ISO 8601 date]`, and `blocked_on_user: false`. Valid approval is explicit user approval, or `ApprovalRecord=workflow-tolerated` from an allowed producer such as `/goal cad` under shared approval-record rules. Go to §7.
- **Any criterion fails**: Keep the current status, document failures, set `blocked_on_user: true`, and align on steps.

---

## 7. Produce Clean Development Document
Transform `.agent/temp/CLARIFY-*.md` into a self-contained, traceable, decisive `.agent/temp/DEVELOP-[task-slug].md` with source metadata for `/develop` staleness checks.

### Transformation Rules

| CLARIFY-*.md section | DEVELOP-*.md section |
|---|---|
| `Discovery & Guidance` -> `Context/Files to Read` | `Implementation Context` -> `Context/Files to Read` |
| `Discovery & Guidance` -> `Context/Files to Read` | `Architecture Guidance` -> `Domain Boundaries` |
| `Discovery & Guidance` -> `Architectural Notes` | `Architecture Guidance` -> `Patterns to Follow` + `Constraints` |
| `Discovery & Guidance` -> `Risks/Gotchas` | `Risk Register` |
| `Assumptions & Open Items` (accepted) | `Risk Register` (as risks with mitigations) |
| Expert-lens findings and answer tradeoffs | Acceptance criteria, `Architecture Guidance` -> `Constraints`, `Risk Register`, or `Priority Stack Validation` |
| `Enrichment Context` -> `Enriched Lineage Snapshot` | `Lineage Notes`, `Executive Summary`, carried-forward/superseded requirements and PBIs, `Architecture Guidance`, `Risk Register`, and `Priority Stack Validation` |

### Document Skeleton
```markdown
---
status: ready-to-develop
title: [Task Title]
created: [ISO 8601 date]
source: .agent/temp/CLARIFY-[task-slug].md
source_status: clarified
source_updated: [ISO 8601 timestamp or file mtime]
source_sha256: [hash of source clarification document]
needs_review: false
stale: false
approval_required: true
approval_record: pending
approval_scope: "/develop .agent/temp/DEVELOP-[task-slug].md"
---

# [Task Title]

## Executive Summary
[2–3 sentences. What, why, success criteria.]

## Lineage Notes
> _Include when the source `CLARIFY-*` continues a previous artifact or contains non-empty enriched lineage evidence._
- **Previous Clarify**: [path/status/hash and what carries forward]
- **Previous Develop**: [path/status/hash and carried-forward implementation guidance]
- **Previous Implementation**: [walkthrough path or commit/ref, changed files, verification/deviation summary]
- **Iteration Delta**: [new changes, superseded decisions, unresolved follow-ups converted to risks or PBIs]

## Requirements
| ID | Type | Description | Acceptance Criteria | Priority |
|---|---|---|---|---|

## Epics
> _Omit if flat backlog (≤4 PBIs, single value area)._
| ID | Title | Description | Requirements |
|---|---|---|---|

## Product Backlog
| ID | Epic | Title | User Story | Acceptance Criteria | Priority | Size | Dependencies | Context |
|---|---|---|---|---|---|---|---|---|

## Implementation Context
- **Context/Files to Read**: [2-4 existing files/directories]
- **Relevant Skills**: [skills actually needed]
- **Verification Permissions**: [build/test allowed or not allowed by user/profile]

## Architecture Guidance
- **Domain Boundaries**: [Affected aggregates, modules, layers]
- **Patterns to Follow**: [Existing patterns to reuse]
- **Integration Points**: [External APIs, shared state, cross-layer effects]
- **Selected Lenses**: [Lenses selected/applied for this task]
- **Constraints**: [Expert Lens Pass findings and tradeoffs that affect implementation]

## Risk Register
| Risk | Impact | Mitigation | Source |
|---|---|---|---|

## Dependency Map
> _Include if PBI count > 4 or cross-PBI dependencies exist._

## Priority Stack Validation
- **Security**: ✅ [brief note]
- **Correctness**: ✅ [brief note]
- **Clarity**: ✅ [brief note]
- **Simplicity**: ✅ [brief note]
- **Performance**: ✅ [brief note]
```
Run `/review .agent/temp/DEVELOP-*.md` before presenting. If no 🔴 Critical findings remain, keep `needs_review: false`; otherwise set `needs_review: true` and block handoff.

Leave `approval_required: true` unless explicit confirmation or a valid `ApprovalRecord=workflow-tolerated` has already been recorded for the exact `/develop` invocation, bounded scope, and risk. When that record exists, set `approval_required: false`, populate `approval_record` and `approval_scope`, and preserve the record for `/develop` validation.

---

## 8. Handoff to `/develop`
Present `.agent/temp/DEVELOP-*.md` and hand off to `/develop`; `/answer` never implements.

| Mode | Action |
|------|--------|
| **Default/manual** | Stop so the user can run `/develop .agent/temp/DEVELOP-[task-slug].md` |
| **`/answer auto`** | Present the document and require explicit "yes" before `/develop`; an allowed producer such as `/goal cad` may satisfy this only under approval-tolerable route rules |
| **Approved skip** | `/develop` may skip its approval phase only with explicit confirmation or a valid `ApprovalRecord=workflow-tolerated` (setting `approval_required: false` in `DEVELOP-*`), provided all criteria are satisfied and no `[ASSUMPTION - unverified]` remains |
| **User changes** | Update `.agent/temp/DEVELOP-*.md` directly only for minor changes, set `needs_review: true`, re-run `/review`, or return to `/clarify` for major changes |

Manual and automatic modes choose how `/develop` is invoked; they never make `/develop` optional after `/clarify`.
`/answer` does not create branches or commits. Carry any iteration-commit plan into `DEVELOP-*` so `/develop`, `/commit-polish`, or explicit git commands can handle VCS work under their gates.

**Standalone `/answer auto` confirmation prompt**:
> ⚠️ **Confirmation required** — `/answer auto` is about to invoke `/develop .agent/temp/DEVELOP-[task-slug].md`.
> Review the document above. Type **yes** to proceed or **no** to stop here.

> [!WARNING]
> `/clarify auto` -> `/answer auto` -> `/develop` after confirmation. Safe only for low-risk tasks: no security, data, schema, public API, dependency, CI/CD, or infrastructure changes; small backlog; clear acceptance criteria; no `[ASSUMPTION - unverified]`. Standalone `/answer auto` requires confirmation; an allowed producer such as `/goal cad` may satisfy it only with an approval-tolerable record.
