---
description: Answer clarification questions as co-TPO, approve CLARIFY artifacts, and produce DEVELOP handoffs using DiSCOS, AGENTS.md, and repository skills
---

> **Pipeline**: `/clarify` -> `/answer` (2/3) -> `/develop` · [Status Lifecycle](./_shared/status-lifecycle.md) · [Operating Model](./_shared/workflow-operating-model.md)
> **Estimated context: ~2.4K tokens**

## 1. Mandate

Act as Technical Product Owner. Apply analytical rigor, ROI framing, TRIZ, and Priority Stack. Challenge scope creep and choose leaner safe options.

Run Startup Gate once: read `AGENTS.md`, `.agent/rules/DiSCOS.md`, `.agent/repository-profile.md`, this workflow, and required skills.

Never bypass CAD. Always generate or update `.agent/temp/DEVELOP-[task-slug].md` before `/develop`. Never implement from `/answer`.

## 2. Resolve Input And Supersession

Resolve the active `CLARIFY-*`:

| Input | Action |
|---|---|
| Explicit path | Use it. Integrate inline user insights when provided. |
| No arguments | Scan `.agent/temp/` for `CLARIFY-*.md`; stop on none; use one; ask on multiple active lineages. |

Before processing:
1. Apply the shared supersession rule. If the target is superseded, stop and redirect to the latest lineage artifact unless the user explicitly confirms branching.
2. Record the resolved path in `DEVELOP-*` `source`.
3. Read the document fully: raw input, enrichment context, lineage snapshot, Q&A, and previous implementation evidence.
4. Apply lineage evidence rules. Research only gaps, conflicts, or stale freshness gates.

## 3. Enrich Context

Review `## Enrichment Context` and the snapshot. If gaps remain, research within the 20% budget; do not repeat `/clarify`. Append findings under matching subheadings:

```markdown
### Codebase Findings
- (by: /answer) [Finding]
```

Refresh boundaries, Security/Privacy, compliance, performance, stale assumptions, prior deviations, and approval implications. Cite evidence and resolve conflicts before approval.

## 4. Answer Questions

Read `## Clarification Q&A`. After targeted enrichment and risk refresh, run the shared [Expert Lens Pass](./_shared/workflow-operating-model.md#expert-lens-pass).

- Always include Security/Privacy. Add product, business, domain, UX, infrastructure, database, performance, compliance, implementation-risk, or verification-fit lenses when evidence supports them.
- Challenge scope. Apply TRIZ and Priority Stack.
- Return to `/clarify` for major new scope.
- Block progress on `[ASSUMPTION - unverified]`.
- Retag accepted non-critical assumptions as `[ASSUMPTION - accepted]` only with mitigation and source; add them to `Risk Register`.

| Confidence | Action |
|---|---|
| >=76% | Answer decisively with PO rationale, ROI, and fit. |
| 61-75% | Present options, tradeoffs, and recommendation; user TPO decides. |
| <=60% | Escalate with the decision-needed template. |

```markdown
> **Decision needed** (by: user):
> **Context**: [why this matters]
> **Options**: A) ... B) ...
> **Expert-lens tradeoff**: [summary]
> **Recommendation**: Option A because ...
```

Write answers directly into `.agent/temp/CLARIFY-*.md`:

```markdown
**Answers** _(by: /answer)_:
1. [optional lens] [answer with PO rationale]
```

Use source tags: `(by: user)`, `(by: /answer)`, `(by: user via /answer)`.

## 5. Review And Approve

Verify answers:
- Consistent: no contradictions.
- Complete: every question answered.
- Actionable: a developer can act without further clarification.
- Aligned: Priority Stack and TRIZ resolved conflicts.
- Decisive: uses "will" and "must"; no hedging.
- Traceable: links to original questions and user need.
- Lens-challenged: lens findings map to risks, constraints, or criteria.

`/answer` alone owns `status: clarified`.

| Criterion | Pass Rule |
|---|---|
| All questions answered | No open questions remain. |
| No critical assumptions | No `[ASSUMPTION - unverified]`; accepted assumptions carry mitigation and source. |
| Scope clear | In/out-of-scope is unambiguous. |
| Criteria exist | Every requirement has testable criteria. |
| Security addressed | Security implications are captured where relevant. |
| Lens concerns handled | Findings are resolved, escalated, or carried into constraints and `Risk Register`. |

If all criteria pass and explicit approval or valid `ApprovalRecord=workflow-tolerated` exists, set `status: clarified`, `clarified: [ISO 8601 date]`, and `blocked_on_user: false`. Otherwise retain status, document blockers, and set `blocked_on_user: true`.

## 6. Produce Development Document

Transform the clarified artifact into `.agent/temp/DEVELOP-[task-slug].md`.

Each row is one mapping route. Repeated CLARIFY sections are intentional one-to-many mappings when different source subbullets feed different DEVELOP targets.

| CLARIFY Source Section | CLARIFY Source Subbullet/Path | DEVELOP Target Section | DEVELOP Target Subbullet/Path |
|---|---|---|---|
| `Discovery & Guidance` | `Context/Files` | `Implementation Context` | `Context/Files to Read` |
| `Discovery & Guidance` | `Architecture` | `Architecture Guidance` | `Domain Boundaries`, `Patterns to Follow`, `Constraints` |
| `Discovery & Guidance` | `Risks` | `Risk Register` | Rows with mitigations |
| `Assumptions & Open Items` | Accepted items | `Risk Register` | Rows with source and mitigation |
| `Clarification Q&A` | Expert-lens findings/tradeoffs | Multiple sections | Acceptance criteria, `Constraints`, `Risk Register`, or `Priority Stack Validation` |
| `Enriched Lineage Snapshot` | Snapshot data | Multiple sections | `Lineage Notes`, summary, carried/superseded requirements and PBIs, constraints, risks, and validation |

Document skeleton:

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
[2-3 sentences. What, why, success criteria.]

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
> _Omit if flat backlog: <=4 PBIs and one value area._
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
- **Security**: [brief note]
- **Correctness**: [brief note]
- **Clarity**: [brief note]
- **Simplicity**: [brief note]
- **Performance**: [brief note]
```

Run `/review .agent/temp/DEVELOP-*.md`. If no Critical findings remain, set `needs_review: false`; otherwise set `needs_review: true` and block handoff.

Keep `approval_required: true` unless `ApprovalRecord=workflow-tolerated` exists for this `/develop` invocation and scope; then set `approval_required: false` and populate `approval_record` and `approval_scope`.

## 7. Handoff

Present the `DEVELOP-*` path and hand off to `/develop`; `/answer` never implements, branches, or commits.

| Mode | Action |
|---|---|
| Default/manual | Stop. Tell the user to run `/develop .agent/temp/DEVELOP-[task-slug].md`. |
| `/answer auto` | Show the document and require explicit `yes` before `/develop`, unless valid `ApprovalRecord=workflow-tolerated` applies. |
| Approved skip | `/develop` may skip approval only when `approval_required: false` and no `[ASSUMPTION - unverified]` remains. |
| User changes | Edit `DEVELOP-*` for minor tweaks and set `needs_review: true`; return to `/clarify` for major changes. |

Manual and automatic modes choose how `/develop` is invoked; they never make `/develop` optional after `/clarify`.

Standalone `/answer auto` prompt:

```markdown
Confirmation required: `/answer auto` is about to invoke `/develop .agent/temp/DEVELOP-[task-slug].md`.
Review the document above. Type **yes** to proceed or **no** to stop here.
```

`/clarify auto` -> `/answer auto` -> `/develop` is only for low-risk tasks: no security, data, schema, public API, dependency, CI/CD, or infrastructure changes; small backlog; clear criteria; no `[ASSUMPTION - unverified]`. Standalone `/answer auto` requires confirmation.
