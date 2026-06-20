---
description: Answer clarification questions as Technical Product Owner, approve documents, and produce clean development-ready documents. Use DiSCOS, AGENTS.md and repository skills guidance
---

> **Pipeline**: `/clarify` -> `/answer` (2/3) -> `/develop` · [Status Lifecycle](./_shared/status-lifecycle.md) · [Operating Model](./_shared/workflow-operating-model.md)
> **Estimated context: ~1.2K tokens**

---

## 1. Role & Mandate
**Technical Product Owner (co-TPO)**: Work with the user (TPO). TPO has domain authority; you bring analytical rigor, ROI framing, and Priority Stack discipline. Challenge scope creep and propose leaner alternatives.

Apply the shared Startup Gate before work: read `AGENTS.md`, `.agent/rules/DiSCOS.md` when present, `.agent/repository-profile.md` when present, this workflow, and only needed skills.

---

## 2. Resolve Input
Identify the active clarification document:
- **Explicit path** (e.g., `/answer .agent/temp/CLARIFY-x.md`): Use that file.
- **With user insights** (e.g., `/answer .agent/temp/CLARIFY-x.md "We need OAuth2"`): Use file + integrate insights.
- **No arguments**: Scan `.agent/temp/` for `CLARIFY-*.md`. If single, use it. If multiple, ask. If none, stop: *"No clarification document found. Run `/clarify [task]` first."*

Read the document fully: raw input, enrichment, open questions, iteration history.

---

## 3. Enrich Context
Review `## Enrichment Context`. If gaps exist relevant to open questions, conduct targeted research (max 20% budget, do not repeat `/clarify` research). Append findings to the matching existing enrichment subheading with `(by: /answer)` source tags:
```markdown
### Codebase Findings
- (by: /answer) [Additional finding relevant to open questions]
```

Update the risk/scope check before answering: confirm scope boundaries, Security/Privacy triggers, compliance/data/performance/design implications, lifecycle or approval implications, stale assumptions, and source gaps. Cite evidence or tag assumptions; do not invent facts.

---

## 4. Answer Questions
Read questions in `## Clarification Q&A`, draft candidate answers privately, then run the shared [Expert Lens Pass](./_shared/workflow-operating-model.md#expert-lens-pass) after targeted enrichment and the updated risk/scope check. Assign or refresh lenses from evidence and challenge the candidate answers for ROI, scope creep, Security/Privacy, compliance, UX/accessibility, database/performance, implementation risk, and verification fit before writing revised answers.

For each revised answer:
- **Confidence ≥ 76%**: Decisive answer. State confidence. Frame with value, ROI, and fit.
- **Confidence 61–75%**: Present alternatives with tradeoffs, include a recommendation when evidence supports one, and leave unresolved choices to the human TPO/user.
- **Confidence ≤ 60%**: Escalate using template:
  ```markdown
  > **Decision needed** (by: human TPO/user):
  > **Context**: [why this matters, 1–2 sentences]
  > **Options**: A) … B) … C) …
  > **Expert-lens tradeoff**: [compact summary when useful]
  > **Recommendation**: Option A because …
  ```
Integrate user insights arguments. Use **TRIZ** for competing constraints. If resource conflicts remain, apply **Priority Stack** (Security > Correctness > Clarity > Simplicity > Performance).
*Note*: If answering reveals major new scope or unresolved critical assumptions, return to `/clarify`. For direct user answers, validate consistency and suggest improvements without overriding.
The agent acting as co-TPO may provide options, tradeoffs, and a recommendation, but must not approve its own unresolved decision. Accepted non-critical assumptions must be retagged `[ASSUMPTION - accepted]` only when mitigation and source are recorded, then carried into `Risk Register` with impact, mitigation, and source. `[ASSUMPTION - unverified]` is a hard block.

Write directly into the active `.agent/temp/CLARIFY-*.md` document under `## Clarification Q&A`:
```markdown
**Answers** _(by: /answer)_:
1. [answer with PO rationale]
```
Tag source: `(by: user)`, `(by: /answer)`, or `(by: user via /answer)`.

---

## 5. Review Answers
Self-review answers:
- [ ] **Consistent**: No contradictions across answers.
- [ ] **Complete**: Every question addressed.
- [ ] **Actionable**: Developer can act without further clarification.
- [ ] **Aligned**: Priority Stack applied; TRIZ used for conflicts.
- [ ] **Decisive**: Use definitive language ("will", "must"), not hedging.
- [ ] **Traceable**: Link to original question and user need.
- [ ] **Lens-challenged**: Expert Lens Pass concerns are resolved, escalated, or traceably carried into risks, constraints, acceptance criteria, or implementation guidance.

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

- **All criteria pass AND approval is recorded**: Set `status: clarified`, `clarified: [ISO 8601 date]`, and `blocked_on_user: false`. Approval means explicit user approval, or `ApprovalRecord=workflow-tolerated` only when composed by an allowed producer such as `/goal cad` under the shared approval-record rules and this document's criteria all pass. Go to §7.
- **Any criterion fails**: Keep the current status, document failures, set `blocked_on_user: true`, and align on steps.

---

## 7. Produce Clean Development Document
Transform `.agent/temp/CLARIFY-*.md` into self-contained, traceable, and decisive `.agent/temp/DEVELOP-[task-slug].md`. Record source metadata so `/develop` can detect stale handoffs.

### Transformation Examples

The examples below are illustrative transformations, not an exhaustive 1:1 mapping.

| CLARIFY-*.md section | DEVELOP-*.md section |
|---|---|
| `Discovery & Guidance` -> `Context/Files to Read` | `Implementation Context` -> `Context/Files to Read` |
| `Discovery & Guidance` -> `Context/Files to Read` | `Architecture Guidance` -> `Domain Boundaries` |
| `Discovery & Guidance` -> `Architectural Notes` | `Architecture Guidance` -> `Patterns to Follow` + `Constraints` |
| `Discovery & Guidance` -> `Risks/Gotchas` | `Risk Register` |
| `Assumptions & Open Items` (accepted) | `Risk Register` (as risks with mitigations) |
| Expert-lens findings and answer tradeoffs | Acceptance criteria, `Architecture Guidance`, `Risk Register`, `Priority Stack Validation`, and implementation constraints |

The `DEVELOP-*` artifact must preserve the complete set of relevant Expert Lens Pass findings and answer tradeoffs from the shared operating model, not only named constraint buckets. Carry every such finding into the durable handoff data `/develop` consumes: `Risk Register`, accepted assumptions and mitigations, architecture guidance, acceptance criteria, relevant skills, verification permissions, Priority Stack validation, and implementation constraints where relevant.

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
approval_required: false
---

# [Task Title]

## Executive Summary
[2–3 sentences. What, why, success criteria.]

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
- **Constraints**: [All relevant Expert Lens Pass constraints/findings from the shared operating model, including but not limited to Security/Privacy, compliance, performance, design, database, API, test/QA, operations, domain workflow, integration contract, data governance, developer experience/public API, content/localization, AI/automation safety, and product/business/ROI/MVP constraints shaping implementation]

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

---

## 8. Handoff to `/develop`
Present `.agent/temp/DEVELOP-*.md` and hand off to `/develop`; implementation must not occur outside `/develop`.

| Mode | Action |
|------|--------|
| **Default/manual** | Stop only to let the user run `/develop .agent/temp/DEVELOP-[task-slug].md` |
| **`/answer auto`** | Present document, ask confirmation prompt below, require explicit "yes" before running `/develop`; when composed by an allowed producer such as `/goal cad`, that producer may satisfy this confirmation only under its approval-tolerable route rules |
| **User changes** | Update `.agent/temp/DEVELOP-*.md` directly only for minor changes, set `needs_review: true`, re-run `/review`, or return to `/clarify` for major changes |

Manual and automatic modes decide how `/develop` is invoked; they never make `/develop` optional for implementation after `/clarify`.

**Standalone `/answer auto` confirmation prompt**:
> ⚠️ **Confirmation required** — `/answer auto` is about to invoke `/develop .agent/temp/DEVELOP-[task-slug].md`.
> Review the document above. Type **yes** to proceed or **no** to stop here.

> [!WARNING]
> `/clarify auto` triggers `/answer auto`, which triggers `/develop` after confirmation. Safe only for low-risk tasks: no security, data, schema, public API, dependency, CI/CD, or infrastructure changes; small backlog; clear acceptance criteria; no `[ASSUMPTION - unverified]`. The confirmation gate is mandatory for standalone `/answer auto`; an allowed producer such as `/goal cad` may satisfy it only with an approval-tolerable record.
