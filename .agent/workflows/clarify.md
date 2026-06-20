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
- **No Artifact Skipping**: Never bypass the CAD workflow sequence. You must always generate the resolved `.agent/temp/CLARIFY-*` artifact. Implementing changes directly without progressing to `/answer` and `/develop` is strictly prohibited.

---

## 2. Capture Raw Input

1. Resolve loop input before deriving the task slug:
   - One existing `.agent/temp/CLARIFY-*.md` path means continue that lineage: set it as `previous_artifact` and treat remaining text as new raw input.
   - Multiple `CLARIFY-*` paths means ask which lineage to continue.
   - Missing or unreadable supplied paths stop creation.
   - If no new text remains, continue from the previous artifact's summary, open items, review/optimization findings, and latest user instruction.
2. Record raw input verbatim under `## Raw Input`; for loop input, include new text plus `Previous artifact:`. If it stays vague after loop context, ask the user to expand.
3. Create a fresh `.agent/temp/CLARIFY-[task-slug].md` unless the user specifies a path. For loop input, use `.agent/temp/CLARIFY-[task-slug]-iteration-[N].md` when the base filename exists.
4. If the target exists and no loop-safe suffix is available, ask to overwrite or rename. Edit a prior artifact in place only on explicit user request and after normal review gates.
5. Set `status: draft`, advancing to `clarifying` when the first question round starts.
6. For loop input, populate lineage metadata in frontmatter: `iteration`, `previous_artifact`, `previous_status`, `previous_updated`, and `previous_sha256`.
7. If a matching prior `.agent/temp/DEVELOP-*.md`, `.agent/temp/WALKTHROUGH-*.md`, or explicit commit/ref is supplied or unambiguously discoverable, record optional lineage metadata: `previous_develop_artifact`, `previous_develop_sha256`, `previous_walkthrough_artifact`, and `previous_commit`. This local temporary loop uses name-based lineage for walkthrough and commit/ref evidence; do not require extra hashes beyond the listed freshness fields.
8. If the task is waiting on the user, keep the current `status` and set `blocked_on_user: true` until the answer is recorded.

---

## 3. Self-Research & Enrichment

Before asking questions, research to minimize round-trips (max 20% budget):
- **Recommended**: Run `/search` to gather context (codebase, KIs, skills, web). Append returned findings to `## Enrichment Context` under the matching subheadings.
- **Fallback**: Manually search files, `.agent/skills/overview-skill-index/SKILL.md`, and external references.
Write findings under `## Enrichment Context` subheadings: Codebase Findings, Knowledge Items, Relevant Skills, External References, Initial Observations.

Then perform an initial risk/scope check and record concise findings under `Initial Observations`: scope boundaries, Security/Privacy triggers, compliance/data/performance/design implications, lifecycle or approval implications, likely specialist-lens triggers, and `[ASSUMPTION - unverified]` items. Cite evidence or tag assumptions; do not invent facts.

For loop input, add `### Enriched Lineage Snapshot` under `## Enrichment Context`. The snapshot must be self-sufficient for `/answer` and sufficient for `/answer` to produce a current `DEVELOP-*` handoff without reopening older artifacts by default. Summarize and cite evidence; do not paste whole prior artifacts or large diffs.

Include these subparts when evidence exists:
- **Previous Clarify Snapshot**: prior artifact path, status, timestamp/mtime, SHA-256, raw intent, summary, accepted answers, requirements, PBIs, risks, assumptions, and open items.
- **Previous Develop Snapshot**: prior `DEVELOP-*` path, source metadata, status, executive summary, implementation context, architecture guidance, constraints, risk register, verification permissions, approval state, and stale/needs-review flags.
- **Previous Implementation Snapshot**: prior walkthrough path or commit/ref, changed files, implemented PBIs or requirements, verification results, deviations, unresolved follow-ups, and any user-approved status changes.
- **Iteration Delta**: requested new change, carried-forward decisions, superseded decisions, changed acceptance criteria, new risks, and downstream handoff notes `/answer` and `/develop` must honor.

Evidence rules:
- Prefer explicit user-supplied `DEVELOP-*`, `WALKTHROUGH-*`, and commit refs. If absent, discover matching prior `DEVELOP-*` artifacts whose `source` points to the previous `CLARIFY-*`; ask if multiple candidates exist.
- Use name-versioned local references as sufficient lineage evidence when they identify one artifact or commit/ref. If a name is missing, ambiguous, or conflicts with summarized evidence, record the source gap or ask.
- Use read-only git inspection for commit evidence. Use an inferred previous commit only when the user explicitly refers to the previous commit and the intended commit is unambiguous; otherwise record the missing commit evidence as an open item.
- Supplied `/review` or `/optimize` findings may be summarized in the snapshot. Route to those workflows only under their gates; do not copy their rules or mutate prior temp artifacts by default.
- If prior implementation evidence conflicts with prior clarification intent, record the conflict and ask unless the user already supplied a clear supersession decision.

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

- **Document selected lenses**: List selected expert lenses once per question round with concise reasoning.
- **Demand references**: Ask for files/URLs when existing designs/docs are mentioned.
- **Flag assumptions**: State explicitly, asking for validation.
- **Rationale**: Give each question an implicit or concise explicit reason tied to business value, security, correctness, usability, compliance, performance, or implementation risk when the reason would otherwise be unclear.
- **Question labels**: If a question is asked by an expert lens, label it with that lens; otherwise leave it unlabeled. Do not group by lens if it weakens priority order.

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
- **Traceability**: Document selected expert lenses in the Q&A section. Label questions asked by expert lenses, and carry relevant findings into answer rationale, requirement sources, acceptance criteria, Discovery & Guidance, Risks/Gotchas, and Priority Stack Validation. Do not add a raw expert transcript.

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

Verify all §8 gates and INVEST pass. Run `/review` on the resolved new `.agent/temp/CLARIFY-*` artifact path; if no 🔴 Critical findings remain, set `status: draft-self-reviewed`.

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
iteration: 1
previous_artifact:
previous_status:
previous_updated:
previous_sha256:
previous_develop_artifact:
previous_develop_sha256:
previous_walkthrough_artifact:
previous_commit:
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
### Enriched Lineage Snapshot
> _Include only when this clarification continues a previous `CLARIFY-*` artifact._
#### Previous Clarify Snapshot
#### Previous Develop Snapshot
#### Previous Implementation Snapshot
#### Iteration Delta
### Initial Observations

## Clarification Q&A
### Round 1
**Selected Expert Lenses:**
- **[Lens Name]**: [Concise reasoning for selecting this lens based on raw input and enrichment context]

**Questions:**
1. [optional lens label] [question]

**Answers** _(by: user | /answer)_:
1. [optional lens label] [answer]

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
| Default/manual | Stop after presenting the resolved new path and tell the user to run `/answer` with that exact path. |
| `/clarify auto` | Invoke `/answer auto` on the new artifact only when autonomy gates allow it. |
| Approved skip | `/answer` may skip its approval phase only with explicit confirmation or a valid `ApprovalRecord=workflow-tolerated`, provided no `[ASSUMPTION - unverified]` remains and all approval criteria are satisfied; it still must produce `DEVELOP-*` before `/develop`. |

For loop input, state that older artifacts are lineage evidence, not the default `/answer` target. `/clarify` does not create branches or commits; record VCS intent as guidance for `/develop` or `/commit-polish`.
