---
description: Clarify a user-given task into requirements as Technical Business Analyst, epics, and product backlog items through iterative questioning. Use DiSCOS, AGENTS.md and repository skills guidance
---

> **Pipeline**: `/clarify` (1/3) → `/answer` → `/develop` · [Status Lifecycle](./_shared/status-lifecycle.md)
> **Estimated context: ~1.2K tokens**

---

## 1. Role & Mandate
**Technical Business Analyst**: Bridge business intent and technical architecture. Research autonomously first, resolve ambiguities, and ensure all requirements are testable.

---

## 2. Capture Raw Input
1. Record user task description verbatim under `## Raw Input`. If < 1 sentence or too vague, ask user to expand.
2. Create document `./CLARIFY-[task-slug].md` at repository root (unless user specifies path).
3. If file exists, ask to overwrite or rename.
4. Set `status: draft`, advancing to `clarifying` when the first question round starts.

---

## 3. Self-Research & Enrichment
Before asking questions, research to minimize round-trips (max 20% budget):
- **Recommended**: Run `/search` to gather context (codebase, KIs, skills, web). Results append to `## Enrichment Context`.
- **Fallback**: Manually search files, `.agent/skills/overview-skill-index/SKILL.md`, and external references.
Write findings under `## Enrichment Context` subheadings: Codebase Findings, Knowledge Items, Relevant Skills, External References, Initial Observations.

---

## 4. First-Principles Analysis
Silently analyze raw input and context:
- **Five Whys** (root purpose) · **MECE** (complete decomposition)
- **Inversion** (what to avoid/fail modes) · **Second-Order Thinking**
- **TRIZ/IFR** (max benefit, zero harm)

---

## 5. Ask Clarification Questions
Batch questions in a single numbered list (5–8 questions per round, prioritizing blockers).
- **Demand references**: Ask for files/URLs when existing designs/docs are mentioned.
- **Flag assumptions**: State explicitly, asking for validation.
- Group by categories as needed (Scope, Constraints, Architecture, Security, etc.).

---

## 6. Iterate Until Clarity
For each round of responses:
1. Integrate answers into the document.
2. Apply First-Principles Analysis to the updated state.
3. Check for remaining gaps:
   - **Gaps exist**: Run follow-up round (back to §5).
   - **No gaps**: Summarize and ask: *"Is this complete and accurate?"*
4. Stop on user confirmation. Max 3 rounds; if reached, tag unknowns `[ASSUMPTION - unverified]`.

---

## 7. Decompose Into Deliverables
- **Requirements**: ID (REQ-NNN), Type (Functional/Non-Functional), Description, Acceptance Criteria, Priority (MoSCoW), Source.
- **Epics**: ID (EPIC-NNN), Title, Description, linked REQs. *Skip if flat backlog (≤4 PBIs, single value area).*
- **PBIs**: ID (PBI-NNN), Epic, Title, User Story, Acceptance Criteria, Priority, Size (S/M/L/XL), Dependencies, Context (skills, files), Assumptions.
- **INVEST Validation**: All PBIs must pass (Independent, Negotiable, Valuable, Estimable, Small, Testable).
- **Assumption Contract**: All `[ASSUMPTION - unverified]` must be resolved before `/develop` starts.

---

## 8. Quality Gate — Priority Stack
Evaluate output top-down. Failure at higher level blocks lower gates.

| Gate | Question |
|---|---|
| **Security** | Security & compliance captured? Data-sensitive PBIs have security criteria? |
| **Correctness** | Requirements trace to user needs? Acceptance criteria testable and unambiguous? |
| **Clarity** | Understandable by someone outside this conversation? |
| **Simplicity** | No unnecessary decomposition? Could items merge without losing clarity? |
| **Performance** | Performance/scalability captured where relevant? |

---

## 9. Output Artifact
Verify all §8 gates and INVEST pass. Set `status: draft-self-reviewed`.

### Pre-presentation Checklist
- [ ] §8 Priority Stack gates pass
- [ ] INVEST valid for all PBIs
- [ ] No unresolved `[ASSUMPTION - unverified]`
- [ ] No scope creep
- [ ] `/review` passed (no 🔴 Critical findings)

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
> _Omit when not applicable._
| ID | Title | Description | Requirements |
|---|---|---|---|

## Product Backlog
| ID | Epic | Title | User Story | Acceptance Criteria | Priority | Size | Dependencies | Context | Assumptions |
|---|---|---|---|---|---|---|---|---|---|

## Dependency Map
> _Include when PBI count > 4 or cross-PBI dependencies exist._

## Priority Stack Validation
- **Security**: ✅/❌ [brief note]
- **Correctness**: ✅/❌ [brief note]
- **Clarity**: ✅/❌ [brief note]
- **Simplicity**: ✅/❌ [brief note]
- **Performance**: ✅/❌ [brief note]
```

---

## 10. Handoff
Present the `draft-self-reviewed` document and stop.
- **Default**: User manually runs `/answer` or `/develop`.
- **`/clarify auto`**: Invokes `/answer auto` immediately after presentation.
- **Skip `/answer`**: Safe only if: no `[ASSUMPTION]`, all PBIs have acceptance criteria, security is addressed, and user explicitly confirms skip. Then run `/develop` directly.
