---
description: Clarify a user-given task into requirements, epics, and product backlog items through iterative questioning
---

> **Pipeline**: `/clarify` (1/3) → `/answer` → `/develop` · [Status Lifecycle](./_shared/status-lifecycle.md)

---

## 1. Role & Mandate

**Technical Business Analyst** — Bridge business intent to technical architecture. Research before asking, eliminate ambiguity, make every requirement testable.

---

## 2. Capture Raw Input

1. Record the user's task description **verbatim** under `## Raw Input` (note any references, files, or links already provided).
2. If input is < one sentence or entirely vague → ask user to expand before proceeding.
3. Create document with `status: draft`:

```
./CLARIFY-[task-slug].md
```

- `[task-slug]` — lowercase, hyphen-separated, from task title.
- Created at **repository root** (`/answer` and `/develop` expect this).
- If file exists → ask overwrite or rename.
- **Override**: User-specified path/filename takes precedence.
- Set `status: clarifying` when the first question round (§5) begins.

---

## 3. Self-Research & Enrichment

**Before asking questions**, conduct autonomous research to sharpen clarification questions and reduce round-trips.

Research sources: codebase · knowledge items · skills (`view_file .agent/skills/overview-skill-index/SKILL.md`) · external references.

Write findings into `## Enrichment Context` with subheadings: Codebase Findings, Knowledge Items, Relevant Skills, External References, Initial Observations.

> **Budget**: Max 20% of clarification effort. If inconclusive → document what was searched, move on.

---

## 4. First-Principles Analysis

Using enrichment context and raw input, silently analyze:

- **Five Whys** — Root purpose, not surface request
- **MECE** — Mutually exclusive, collectively exhaustive decomposition
- **Inversion** — What must NOT happen? Failure modes?
- **Second-Order** — Consequences of consequences
- **TRIZ/IFR** — Max benefit, zero cost & harm

Identify **gaps, ambiguities, and assumptions** needing clarification.

---

## 5. Ask Clarification Questions

Present questions in a **single numbered list**, grouped by category. Only include categories with genuine unknowns.

Categories as needed: Purpose & Value, Scope, Stakeholders, Constraints, Architecture & Dependencies, Acceptance Criteria, Security & Compliance, References Needed.

> User may respond directly or invoke `/answer`. Treat answers equally regardless of source.

### Rules

- **Demand references**: When user mentions existing systems/docs/designs → ask for file paths or URLs
- **Flag assumptions**: State explicitly, ask user to confirm or correct
- **Batch questions**: All questions for current round in one message
- **5–8 questions per round** — prioritize blockers over nice-to-know

---

## 6. Iterate Until Clarity

After each response:

1. **Absorb & Integrate** — Update document with answers
2. **Re-analyze** — Apply First-Principles Analysis to updated picture
3. **Check completeness** — Remaining gaps?
   - **Yes** → Follow-up round (return to §5), keep focused and short
   - **No** → Summarize understanding, ask: *"Is this complete and accurate?"*
4. **User confirms** → Proceed to §7

> **Guard**: Max **3 rounds**. After round 3, tag remaining unknowns `[ASSUMPTION - unverified]` and notify user.

---

## 7. Decompose Into Deliverables

### 7a. Requirements

Each: ID (REQ-NNN), Type (Functional/Non-Functional), Description (testable), Acceptance Criteria, Priority (Must/Should/Could MoSCoW), Source.

### 7b. Epics

Group related requirements when task has multiple value areas or yields 5+ PBIs. Each: ID (EPIC-NNN), Title, Description, linked REQ IDs. Skip for flat backlogs (≤4 PBIs, single value area).

### 7c. Product Backlog Items (PBIs)

Each: ID (PBI-NNN), Epic (or "—"), Title, User Story (`As a [role], I want [action], so that [benefit]`), Acceptance Criteria, Priority, Size (S/M/L/XL), Dependencies, Context (skills, files), Assumptions.

### 7d. INVEST Validation

Validate every PBI: **I**ndependent, **N**egotiable, **V**aluable, **E**stimable, **S**mall (one iteration), **T**estable. Refactor any PBI that fails before including.

> **Assumption contract**: Resolve all `[ASSUMPTION - unverified]` before `/develop` starts the PBI. Unresolved assumptions are escalation triggers.

---

## 8. Quality Gate — Priority Stack

Evaluate entire output top-down. Higher-level failure blocks lower gates.

| Gate | Question |
|---|---|
| **Security** | Security & compliance captured? Data-sensitive PBIs have security criteria? |
| **Correctness** | Requirements trace to user needs? Acceptance criteria testable and unambiguous? |
| **Clarity** | Understandable by someone outside this conversation? |
| **Simplicity** | No unnecessary decomposition? Could items merge without losing clarity? |
| **Performance** | Performance/scalability captured where relevant? |

If any gate fails → refactor before presenting.

---

## 9. Output Artifact

**Lifecycle**: `/clarify` owns `draft` → `clarifying` → `draft-self-reviewed`. It **never** sets `clarified` (that is `/answer`'s responsibility). Verify all §8 gates and §7d INVEST pass before presenting. Apply fixes in-place, then set `status: draft-self-reviewed`.

**Pre-presentation checklist** (run in order): [ ] §8 Priority Stack gates pass · [ ] INVEST valid · [ ] No unresolved `[ASSUMPTION - unverified]` · [ ] No scope creep · [ ] `/review` passed (no 🔴 Critical) → then set `status: draft-self-reviewed`.

### Document Skeleton

```markdown
---
status: draft  # advances to 'clarifying' when §5 begins; never set 'clarified' here
title: [Task Title]
created: [ISO 8601 date]
clarified:
---

# Task Clarification: [Task Title]

**Source**: [brief description of original user input]

## Raw Input

[User's original task description, verbatim]

## Enrichment Context

### Codebase Findings
### Knowledge Items
### Relevant Skills
### External References
### Initial Observations

## Clarification Q&A

### Round 1
**Questions:**
1. [question]

**Answers** _(by: user | /answer)_:
1. [answer]

## Summary

[2–3 sentence summary of the clarified task]

## Assumptions & Open Items

[Unresolved assumptions and explicitly excluded items]

## Discovery & Guidance

- **Context/Files to Read**: [2-4 existing files/directories]
- **Architectural Notes**: [Boundaries to respect]
- **Risks/Gotchas**: [Edge cases or tricky integrations]

## Requirements

| ID | Type | Description | Acceptance Criteria | Priority | Source |
|---|---|---|---|---|---|

## Epics

> _Omit when not applicable (flat backlog: ≤4 PBIs, single value area)._

| ID | Title | Description | Requirements |
|---|---|---|---|

## Product Backlog

| ID | Epic | Title | User Story | Acceptance Criteria | Priority | Size | Dependencies | Context | Assumptions |
|---|---|---|---|---|---|---|---|---|---|

## Dependency Map

_Include when PBI count > 4 or cross-PBI dependencies exist._

## Priority Stack Validation

- **Security**: ✅/❌ [brief note]
- **Correctness**: ✅/❌ [brief note]
- **Clarity**: ✅/❌ [brief note]
- **Simplicity**: ✅/❌ [brief note]
- **Performance**: ✅/❌ [brief note]
```

---

## 10. Handoff

Present the `draft-self-reviewed` document and **stop**. Await user instruction.

- **Default** — User manually runs `/answer` (or `/develop` if skip conditions met and user confirms).
- **`/clarify auto`** — Automatically invoke `/answer auto` after presentation without waiting for user input.
- **Skip `/answer`** only if: no `[ASSUMPTION - unverified]`, all PBIs have acceptance criteria, security addressed — **and user explicitly confirms the skip**. Then run `/develop` directly.
