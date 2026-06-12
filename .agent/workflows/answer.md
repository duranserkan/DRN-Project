---
description: Answer clarification questions as Technical Product Owner, approve documents, and produce clean development-ready documents. Use DiSCOS, AGENTS.md and repository skills guidance
---

> **Pipeline**: `/clarify` → `/answer` (2/3) → `/develop` · [Status Lifecycle](./_shared/status-lifecycle.md)
> **Estimated context: ~1.2K tokens**

---

## 1. Role & Mandate
**Technical Product Owner (co-TPO)**: Work with the user (TPO). TPO has domain authority; you bring analytical rigor, ROI framing, and Priority Stack discipline. Challenge scope creep and propose leaner alternatives.

---

## 2. Resolve Input
Identify the active clarification document:
- **Explicit path** (e.g., `/answer .agent/temp/CLARIFY-x.md`): Use that file.
- **With user insights** (e.g., `/answer .agent/temp/CLARIFY-x.md "We need OAuth2"`): Use file + integrate insights.
- **No arguments**: Scan `.agent/temp/` for `CLARIFY-*.md`. If single, use it. If multiple, ask. If none, stop: *"No clarification document found. Run `/clarify [task]` first."*

Read the document fully: raw input, enrichment, open questions, iteration history.

---

## 3. Enrich Context
Review `## Enrichment Context`. If gaps exist relevant to open questions, conduct targeted research (max 20% budget, do not repeat `/clarify` research). Append to the active `.agent/temp/CLARIFY-*.md` document:
```markdown
### Supplementary Context _(by: /answer)_
- [Additional findings relevant to open questions]
```

---

## 4. Answer Questions
Read questions in `## Clarification Q&A`. For each:
- **Confidence ≥ 76%**: Decisive answer. State confidence. Frame with value, ROI, and fit.
- **Confidence 61–75%**: Present alternatives with tradeoffs. Let co-TPO choose.
- **Confidence ≤ 60%**: Escalate using template:
  ```markdown
  > **Decision needed** (by: co-TPO):
  > **Context**: [why this matters, 1–2 sentences]
  > **Options**: A) … B) … C) …
  > **Recommendation**: Option A because …
  ```
Integrate user insights arguments. Use **TRIZ** for competing constraints. If resource conflicts remain, apply **Priority Stack** (Security > Correctness > Clarity > Simplicity > Performance).
*Note*: If answering reveals major new scope, return to `/clarify`. For direct user answers, validate consistency and suggest improvements without overriding.

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

---

## 6. Approval Decision
`/answer` is the sole owner of `status: clarified`.

| Criterion | Check |
|---|---|
| **All questions answered** | No open questions remain |
| **No critical assumptions** | All `[ASSUMPTION - unverified]` resolved or accepted |
| **Scope is clear** | In/out-of-scope unambiguous |
| **Acceptance criteria exist** | Every requirement has testable criteria |
| **Security addressed** | Security implications captured where relevant |

- **All met** / **User explicitly approves**: Set `status: clarified` and `clarified: [ISO 8601 date]`. Go to §7.
- **Partially met**: Document failures, notify user (BlockedOnUser: true), and align on steps.

---

## 7. Produce Clean Development Document
Transform `.agent/temp/CLARIFY-*.md` into self-contained, traceable, and decisive `.agent/temp/DEVELOP-[task-slug].md`.

### Mapping Table
| CLARIFY-*.md section | DEVELOP-*.md section |
|---|---|
| `Discovery & Guidance` → `Context/Files to Read` | `Architecture Guidance` → `Domain Boundaries` |
| `Discovery & Guidance` → `Architectural Notes` | `Architecture Guidance` → `Patterns to Follow` + `Constraints` |
| `Discovery & Guidance` → `Risks/Gotchas` | `Risk Register` |
| `Assumptions & Open Items` (accepted) | `Risk Register` (as risks with mitigations) |

### Document Skeleton
```markdown
---
status: ready-to-develop
title: [Task Title]
created: [ISO 8601 date]
source: .agent/temp/CLARIFY-[task-slug].md
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

## Architecture Guidance
- **Domain Boundaries**: [Affected aggregates, modules, layers]
- **Patterns to Follow**: [Existing patterns to reuse]
- **Integration Points**: [External APIs, shared state, cross-layer effects]
- **Constraints**: [Technical constraints shaping implementation]

## Risk Register
| Risk | Impact | Mitigation |
|---|---|---|

## Dependency Map
> _Include if PBI count > 4 or cross-PBI dependencies exist._

## Priority Stack Validation
- **Security**: ✅ [brief note]
- **Correctness**: ✅ [brief note]
- **Clarity**: ✅ [brief note]
- **Simplicity**: ✅ [brief note]
- **Performance**: ✅ [brief note]
```
Run `/review` on `.agent/temp/DEVELOP-*.md` before presenting (must be ✅ or ⚠️).

---

## 8. Handoff to `/develop`
Present `.agent/temp/DEVELOP-*.md` and stop.

| Mode | Action |
|------|--------|
| **Default** | User manually runs `/develop .agent/temp/DEVELOP-[task-slug].md` |
| **`/answer auto`** | Present document, ask confirmation prompt below, require explicit "yes" before running `/develop` |
| **User changes** | Update `.agent/temp/DEVELOP-*.md` directly (minor) or return to `/clarify` (major) |

**`/answer auto` confirmation prompt**:
> ⚠️ **Confirmation required** — `/answer auto` is about to invoke `/develop .agent/temp/DEVELOP-[task-slug].md`.
> Review the document above. Type **yes** to proceed or **no** to stop here.

> [!WARNING]
> `/clarify auto` triggers `/answer auto`, which triggers `/develop` after confirmation. Safe only for low-risk, well-understood tasks. The confirmation gate is mandatory.
