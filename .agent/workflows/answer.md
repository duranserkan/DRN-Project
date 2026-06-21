---
description: Answer clarification questions as co-TPO, approve CLARIFY artifacts, and produce DEVELOP handoffs using DiSCOS, AGENTS.md, and repository skills
---

> **Pipeline**: `/clarify` -> `/answer` (2/3) -> `/develop` · [Status Lifecycle](./_shared/status-lifecycle.md) · [Operating Model](./_shared/workflow-operating-model.md)
> **Estimated context: ~1.8K tokens**

---

## 1. Role & Mandate
**Technical Product Owner (co-TPO)**: Supply analytical rigor, ROI framing, and Priority Stack discipline. Challenge scope creep and suggest leaner alternatives.

**Startup Gate**: Read `AGENTS.md`, `.agent/rules/DiSCOS.md`, `.agent/repository-profile.md`, this workflow, and required skills.

- **No Artifact Skipping**: Always generate or update `.agent/temp/DEVELOP-[task-slug].md` before `/develop`. Never transition directly to code edits or implementation from `/answer`.

---

## 2. Resolve Input & Supersession
Resolve active `CLARIFY-*` artifact:
- **Explicit path**: Use targeted file. Integrate user insights if provided (e.g., `/answer [path] "[insights]"`).
- **No arguments**: Scan `.agent/temp/` for `CLARIFY-*.md`. Stop if none found. If one, use it. If multiple, apply §2a.

### 2a. Supersession Gate
Check lineage metadata before processing:
- Apply shared supersession rule. If target is superseded, stop and redirect to the latest lineage artifact unless the user explicitly confirms branching.
- Ask user to resolve ambiguity if no-argument scanning leaves multiple active lineages.
- Record resolved path in `DEVELOP-*` `source` field.
- Read document fully: raw input, enrichment context, snapshot, questions, and previous implementation evidence.
- Apply lineage evidence rules. Research gaps, conflicts, or stale freshness gates only.

---

## 3. Enrich Context
Review `## Enrichment Context` and snapshot. If gaps exist, research (max 20% budget; do not repeat `/clarify`) and append findings under matching subheadings:
```markdown
### Codebase Findings
- (by: /answer) [Finding]
```

Refresh risk/scope checklist: boundaries, Security/Privacy, compliance, performance, stale assumptions, and prior deviations. Cite evidence; resolve conflicts before approval.

---

## 4. Answer Questions
Read `## Clarification Q&A`. Run [Expert Lens Pass](./_shared/workflow-operating-model.md#expert-lens-pass) after targeted enrichment and the updated risk/scope check. Select lenses from evidence: Security/Privacy is mandatory; add product, business, domain, UX, infrastructure, database, performance, compliance, implementation-risk, or verification-fit lenses when applicable. Challenge scope.

| Confidence | Action |
|---|---|
| ≥ 76% | Decisive answer with PO rationale, ROI, and fit. |
| 61-75% | Present options, tradeoffs, and recommendation; user TPO decides. |
| ≤ 60% | Escalate using the template below. |

```markdown
> **Decision needed** (by: user):
> **Context**: [why this matters]
> **Options**: A) ... B) ...
> **Expert-lens tradeoff**: [summary]
> **Recommendation**: Option A because ...
```

Apply TRIZ and Priority Stack. Return to `/clarify` for major new scope. Unverified assumptions (`[ASSUMPTION - unverified]`) block progress. Retag accepted assumptions as `[ASSUMPTION - accepted]` only with mitigation and source, then add them to `Risk Register`.

Write answers directly to `.agent/temp/CLARIFY-*.md` under `## Clarification Q&A`:
```markdown
**Answers** _(by: /answer)_:
1. [optional lens] [answer with PO rationale]
```
Tag sources: `(by: user)`, `(by: /answer)`, or `(by: user via /answer)`.

---

## 5. Review Answers
Verify answers meet these checklist items:
- [ ] **Consistent**: No contradictions.
- [ ] **Complete**: Every question answered.
- [ ] **Actionable**: Developer can act immediately without further clarification.
- [ ] **Aligned**: Priority Stack and TRIZ applied to conflicts.
- [ ] **Decisive**: Uses "will" and "must" (no hedging).
- [ ] **Traceable**: Links to original questions and user need.
- [ ] **Lens-challenged**: Lens findings mapped to risks, constraints, or criteria.

---

## 6. Approval Decision
`/answer` is the sole owner of `status: clarified`.

| Criterion | Check |
|---|---|
| **All questions answered** | No open questions remain |
| **No critical assumptions** | All `[ASSUMPTION - unverified]` resolved; accepted assumptions retagged `[ASSUMPTION - accepted]` with mitigation and source |
| **Scope is clear** | In/out-of-scope unambiguous |
| **Acceptance criteria exist** | Every requirement has testable criteria |
| **Security addressed** | Security implications captured where relevant |
| **Lens concerns handled** | Lens findings resolved, escalated, or carried into constraints and `Risk Register` |

- **Pass (All criteria met & approval recorded)**: Set `status: clarified`, `clarified: [ISO 8601 date]`, and `blocked_on_user: false`. Proceed to §7. (Approval must be explicit or `ApprovalRecord=workflow-tolerated`).
- **Fail (Any criterion unmet)**: Retain current status, document blockers, set `blocked_on_user: true`.

---

## 7. Produce Clean Development Document
Transform `.agent/temp/CLARIFY-*.md` into a self-contained, decisive `.agent/temp/DEVELOP-[task-slug].md`.

### Transformation Rules

> [!NOTE]
> The table below maps sections and subbullets from `CLARIFY-*.md` to their corresponding destinations in `DEVELOP-*.md`. A single parent section (such as `Discovery & Guidance`) can map to multiple distinct target sections/subbullets (one-to-many mapping) to logically organize the development context.

| CLARIFY Source Section | CLARIFY Source Subbullet | DEVELOP Target Section | DEVELOP Target Subbullet |
|---|---|---|---|
| `Discovery & Guidance` | `Context/Files` | `Implementation Context` | `Context/Files to Read` |
| `Discovery & Guidance` | `Architecture` | `Architecture Guidance` | `Domain Boundaries` |
| `Discovery & Guidance` | `Architecture` | `Architecture Guidance` | `Patterns to Follow` + `Constraints` |
| `Discovery & Guidance` | `Risks` | `Risk Register` | (All rows) |
| `Assumptions & Open Items` | (Accepted items) | `Risk Register` | (With mitigations) |
| `Clarification Q&A` | (Expert-lens findings / tradeoffs) | Multiple sections | Acceptance criteria, `Constraints`, `Risk Register`, or `Priority Stack Validation` |
| `Enriched Lineage Snapshot` | (All snapshot data) | Multiple sections | `Lineage Notes`, `Executive Summary`, carried-forward/superseded requirements and PBIs, `Architecture Guidance`, `Risk Register`, and `Priority Stack Validation` |

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
> _Include when the source `CLARIFY-*` continues a previous artifact or contains enriched lineage evidence._
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

Run `/review .agent/temp/DEVELOP-*.md`. If no 🔴 Critical findings remain, set `needs_review: false`; otherwise set to `true` and block handoff.

Keep `approval_required: true` unless `ApprovalRecord=workflow-tolerated` exists for this `/develop` invocation and scope; then set `approval_required: false` and populate `approval_record`/`approval_scope`.

---

## 8. Handoff to `/develop`
Present `DEVELOP-*.md` and hand off to `/develop`; `/answer` never implements, branches, or commits.

| Mode | Action |
|------|--------|
| **Default/manual** | Stop. User runs `/develop .agent/temp/DEVELOP-[task-slug].md`. |
| **`/answer auto`** | Show document; require explicit "yes" before `/develop` (unless bypassed by `ApprovalRecord=workflow-tolerated` under `/goal cad`). |
| **Approved skip** | `/develop` skips approval only if `approval_required: false` in `DEVELOP-*.md` and no `[ASSUMPTION - unverified]` remains. |
| **User changes** | Edit `DEVELOP-*.md` directly for minor tweaks and set `needs_review: true`; return to `/clarify` for major changes. |

Manual and automatic modes choose how `/develop` is invoked; they never make `/develop` optional after `/clarify`.

**Standalone `/answer auto` confirmation prompt**:
> ⚠️ **Confirmation required** — `/answer auto` is about to invoke `/develop .agent/temp/DEVELOP-[task-slug].md`.
> Review the document above. Type **yes** to proceed or **no** to stop here.

> [!WARNING]
> `/clarify auto` -> `/answer auto` -> `/develop` is for low-risk tasks only: no security, data, schema, public API, dependency, CI/CD, or infrastructure changes; small backlog; clear acceptance criteria; no `[ASSUMPTION - unverified]`. Standalone `/answer auto` requires confirmation.
