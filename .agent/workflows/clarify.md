---
description: Clarify a user-given task into requirements as Technical Business Analyst, epics, and product backlog items through iterative questioning. Use DiSCOS, AGENTS.md and repository skills guidance
---

> **Pipeline**: `/clarify` (1/3) -> `/answer` -> `/develop` · [Status Lifecycle](./_shared/status-lifecycle.md) · [Operating Model](./_shared/workflow-operating-model.md)
> **Estimated context: ~1.2K tokens**

---

## 1. Role & Mandate

**Technical Business Analyst**: Bridge business intent and technical architecture. Research autonomously first, resolve ambiguities, and ensure all requirements are testable.

Apply the shared Startup Gate before work: read `AGENTS.md`, `.agent/rules/DiSCOS.md` when present, `.agent/repository-profile.md` when present, this workflow, and only needed skills.

## 1a. Flow Rules

- **Context Reuse**: For repeated startup/profile context in the same session, reuse conclusions; re-read only changed or missing sources.
- **Handling Uncertainty**: Record an accepted non-critical assumption only when allowed, and ensure it is documented with its source and mitigation.
- **Stop-and-Ask**: Stop and ask the user (or route to the owning workflow) if there is unclear scope, confidence is below the threshold, a security-sensitive choice arises, destructive/VCS actions are needed, a gate fails, a source is stale, or an unresolved `[ASSUMPTION - unverified]` occurs.

---

## 2. Capture Raw Input

1. Record user task description verbatim under `## Raw Input`. If < 1 sentence or too vague, ask user to expand.
2. Create document `.agent/temp/CLARIFY-[task-slug].md` (unless user specifies path).
3. If file exists, ask to overwrite or rename.
4. Set `status: draft`, advancing to `clarifying` when the first question round starts.
5. If the task is waiting on the user, keep the current `status` and set `blocked_on_user: true` until the answer is recorded.

---

## 3. Self-Research & Enrichment

Before asking questions, research to minimize round-trips (max 20% budget):
- **Recommended**: Run `/search` to gather context (codebase, KIs, skills, web). Append returned findings to `## Enrichment Context` under the matching subheadings.
- **Fallback**: Manually search files, `.agent/skills/overview-skill-index/SKILL.md`, and external references.
Write findings under `## Enrichment Context` subheadings: Codebase Findings, Knowledge Items, Relevant Skills, External References, Initial Observations.

Then perform an initial risk/scope check and record concise findings under `Initial Observations`: scope boundaries, Security/Privacy triggers, compliance/data/performance/design implications, lifecycle or approval implications, likely specialist-lens triggers, and `[ASSUMPTION - unverified]` items. Cite evidence or tag assumptions; do not invent facts.

---

## 4. First-Principles Analysis

Silently analyze raw input and context:
- **Five Whys** (root purpose) · **MECE** (complete decomposition)
- **Inversion** (what to avoid/fail modes) · **Second-Order Thinking**
- **TRIZ/IFR** (max benefit, zero harm)

---

## 5. Expert Lens Pass & Ask Clarification Questions

Before each question round, run the shared [Expert Lens Pass](./_shared/workflow-operating-model.md#expert-lens-pass) after §3 and §4 are current. Assign lenses from gathered evidence, not raw input alone.

Use the pass to improve question quality, missing context discovery, scope boundaries, MVP/ROI pressure, Security/Privacy and compliance coverage, domain workflow realism, acceptance-criteria candidates, and risk or assumption discovery.

- **Demand references**: Ask for files/URLs when existing designs/docs are mentioned.
- **Flag assumptions**: State explicitly, asking for validation.
- **Rationale**: Give each question an implicit or concise explicit reason tied to business value, security, correctness, usability, compliance, performance, or implementation risk when the reason would otherwise be unclear.
- **Labels**: Use persona/lens labels only when they make a question clearer. Do not group by persona or category if it weakens priority order.

---

## 6. Iterate Until Clarity

For each round of responses:
1. Integrate answers into the document.
2. Apply First-Principles Analysis to the updated state.
3. Refresh the risk/scope check. Reuse the previous lens set unless the risk profile changed; when it changed, reassign lenses through §5 before generating follow-up questions.
4. Check for remaining gaps:
   - **Gaps exist**: Run follow-up round (back to §5).
   - **No gaps**: Summarize and ask: *"Is this complete and accurate?"*
5. Stop on user confirmation. When composed by an allowed producer such as `/goal cad`, `ApprovalRecord=workflow-tolerated` may satisfy this completeness confirmation only if the route is approval-tolerable and no gaps remain. Max 3 rounds; if reached, tag unknowns `[ASSUMPTION - unverified]`, keep `status: clarifying`, set `blocked_on_user: true`, and do not produce `draft-self-reviewed`.

---

## 7. Decompose Into Deliverables

- **Requirements**: ID (REQ-NNN), Type (Functional/Non-Functional), Description, Acceptance Criteria, Priority (MoSCoW), Source.
- **Epics**: ID (EPIC-NNN), Title, Description, linked REQs. *Skip if flat backlog (≤4 PBIs, single value area).*
- **PBIs**: ID (PBI-NNN), Epic, Title, User Story, Acceptance Criteria, Priority, Size (S/M/L/XL), Dependencies, Context (skills, files), Assumptions.
- **INVEST Validation**: All PBIs must pass (Independent, Negotiable, Valuable, Estimable, Small, Testable).
- **Assumption Contract**: `[ASSUMPTION - unverified]` blocks `draft-self-reviewed`; accepted non-critical assumptions must be retagged `[ASSUMPTION - accepted]` and carried to the risk register by `/answer`.
- **Traceability**: Carry Expert Lens Pass findings into questions, answer rationale, requirement sources, acceptance criteria, Discovery & Guidance, Risks/Gotchas, and Priority Stack Validation. Do not add a raw expert transcript.

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

Also confirm Expert Lens Pass outputs are traceably reflected in durable artifact fields where they changed the requirements, risks, constraints, acceptance criteria, or question rationale.

---

## 9. Output Artifact

Verify all §8 gates and INVEST pass. Run `/review .agent/temp/CLARIFY-[task-slug].md`; if no 🔴 Critical findings remain, set `status: draft-self-reviewed`.

### Pre-presentation Checklist

- [ ] §8 Priority Stack gates pass
- [ ] INVEST valid for all PBIs
- [ ] No unresolved `[ASSUMPTION - unverified]`
- [ ] No scope creep
- [ ] `/review` passed (no 🔴 Critical findings)

### Document Skeleton

```markdown
---
status: draft  # advances to 'clarifying' when §5 begins, and to 'draft-self-reviewed' after §9 checklist and /review pass; never set 'clarified' here
title: [Task Title]
created: [ISO 8601 date]
clarified:
blocked_on_user: false
needs_review: false
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
[2-3 sentence summary]

## Assumptions & Open Items
[accepted assumptions, unresolved questions, exclusions]

## Discovery & Guidance
- **Context/Files to Read**: [2-4 files/directories]
- **Architectural Notes**: [boundaries]
- **Risks/Gotchas**: [edge cases]

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
- **Security**:
- **Correctness**:
- **Clarity**:
- **Simplicity**:
- **Performance**:
```

---

## 10. Handoff

Present the `draft-self-reviewed` artifact and route to `/answer`. `/clarify` never authorizes target-file mutation.

| Mode | Action |
|---|---|
| Default/manual | Stop after presenting the path and tell the user to run `/answer .agent/temp/CLARIFY-[task-slug].md`. |
| `/clarify auto` | Invoke `/answer auto` only when autonomy gates allow it. |
| Approved skip | `/answer` may skip its approval phase only with explicit confirmation or a valid `ApprovalRecord=workflow-tolerated`, provided no `[ASSUMPTION - unverified]` remains and all approval criteria are satisfied; it still must produce `DEVELOP-*` before `/develop`. |