---
description: Clarify a user-given task into requirements, epics, and product backlog items through iterative questioning
---

## 1. Capture Raw Input

- Record the user's task description verbatim.
- Note any references, files, or links already provided.
- If the input is **less than one sentence** or entirely vague, ask the user to expand before proceeding.

---

## 2. First-Principles Analysis

Before asking questions, silently analyze the raw input using DiSCOS mental models:

| Model | Apply |
|---|---|
| **Five Whys** | Why is this needed? Dig to root purpose. |
| **MECE Decomposition** | Break the task into mutually exclusive, collectively exhaustive parts. |
| **Inversion** | What must NOT happen? What failure modes exist? |
| **Second-Order Thinking** | What are the consequences of the consequences? |
| **Ideal Final Result (TRIZ)** | What does the perfect outcome look like — max benefit, zero cost & harm? |

Use the analysis to identify **gaps, ambiguities, and assumptions** that need clarification.

---

## 3. Ask Clarification Questions

Present questions to the user in a **single numbered list**, grouped by category. Only include categories with genuine unknowns — skip categories already answered by the raw input.

### Question Categories

| Category | Example Questions |
|---|---|
| **Purpose & Value** | What problem does this solve? Who benefits? What is the expected outcome? |
| **Scope & Boundaries** | What is explicitly in scope? What is explicitly out of scope? Are there phases? |
| **Stakeholders & Users** | Who are the end users? Who approves? Who is impacted? |
| **Constraints** | Timeline? Budget? Technology restrictions? Regulatory? Existing system limitations? |
| **Dependencies** | What must exist first? What external systems are involved? Are there blocking items? |
| **Acceptance Criteria** | How will we know this is done? What does "good" look like? Measurable success metrics? |
| **Security & Compliance** | Data sensitivity? Authentication/authorization needs? Audit requirements? |
| **References Needed** | Existing docs, designs, APIs, or codebases the agent should read to understand context? |

### Rules

- **Demand references**: When the user mentions existing systems, documents, designs, or prior work, explicitly ask for file paths, URLs, or relevant code references. Do not proceed on assumption.
- **Flag assumptions**: If you must assume something to proceed, state the assumption explicitly and ask the user to confirm or correct.
- **Batch questions**: Ask all questions for the current round in one message. Do not drip-feed.
- **Prioritize**: Order questions from most critical (blockers) to least critical (nice-to-know).

---

## 4. Iterate Until Clarity

After each user response:

1. **Absorb** — Integrate answers into your understanding.
2. **Re-analyze** — Apply First-Principles Analysis (Step 2) to the updated picture.
3. **Check completeness** — Are there remaining gaps?
   - **Yes** → Ask a follow-up round (return to Step 3). Keep follow-up rounds focused and short.
   - **No** → Summarize your understanding back to the user and ask: *"Is this complete and accurate? Anything to add or correct?"*
4. **User confirms** → Proceed to Step 5.

> **Guard**: Maximum **3 clarification rounds**. If significant ambiguity remains after 3 rounds, document the unknowns as explicit assumptions and proceed — flag them in the output artifact.

---

## 5. Decompose Into Deliverables

Using the clarified understanding, produce the following structured decomposition:

### 5a. Requirements

For each requirement:

| Field | Content |
|---|---|
| **ID** | REQ-001, REQ-002, … |
| **Type** | Functional / Non-Functional |
| **Description** | Clear, testable statement |
| **Acceptance Criteria** | Specific, measurable conditions for "done" |
| **Priority** | Must / Should / Could (MoSCoW) |
| **Source** | Which user statement or reference this derives from |

### 5b. Epics

Group related requirements into Epics when:
- The task is large enough to warrant decomposition (multiple distinct user-value areas or domain boundaries)
- A single flat backlog would exceed ~8 PBIs

For each Epic:

| Field | Content |
|---|---|
| **ID** | EPIC-001, EPIC-002, … |
| **Title** | Short, value-oriented name |
| **Description** | What this epic delivers and why |
| **Requirements** | List of REQ IDs covered |

> If the task is small enough for a flat backlog (≤8 PBIs, single value area), skip epics and produce PBIs directly under requirements.

### 5c. Product Backlog Items (PBIs)

For each PBI:

| Field | Content |
|---|---|
| **ID** | PBI-001, PBI-002, … |
| **Epic** | Parent EPIC ID (or "—" if no epics) |
| **Title** | Action-oriented summary |
| **User Story** | As a [role], I want [action], so that [benefit] |
| **Acceptance Criteria** | Checklist of specific, testable conditions |
| **Priority** | Must / Should / Could |
| **Size** | S / M / L / XL (relative effort) |
| **Dependencies** | Other PBI IDs this depends on |
| **Assumptions** | Any unverified assumptions carried forward |

### INVEST Validation

Before finalizing, validate every PBI against INVEST:

| Criterion | Check |
|---|---|
| **I**ndependent | Can be developed without requiring other PBIs to be in progress simultaneously? |
| **N**egotiable | Leaves room for implementation decisions? |
| **V**aluable | Delivers identifiable value to a user or stakeholder? |
| **E**stimable | Clear enough to size? |
| **S**mall | Completable within one iteration/sprint? If not, split. |
| **T**estable | Acceptance criteria are specific and verifiable? |

If a PBI fails any criterion, refactor it (split, clarify, or merge) before including.

---

## 6. Quality Gate — Priority Stack

Evaluate the entire output top-down. A higher-level failure blocks lower gates.

| Gate | Question |
|---|---|
| **Security** | Are security & compliance requirements explicitly captured? Any data-sensitive PBI missing security criteria? |
| **Correctness** | Do requirements trace back to stated user needs? Are acceptance criteria testable and unambiguous? |
| **Clarity** | Can someone outside this conversation understand every item without additional context? |
| **Simplicity** | Is there unnecessary decomposition? Could items be merged without losing clarity? |
| **Performance** | Are performance/scalability requirements captured where relevant? |

If any gate fails, refactor the output before presenting.

---

## 7. Output Artifact

### File Location & Naming

Save the output to the **repository root** as:

```
CLARIFY-[task-slug].md
```

- `[task-slug]` — lowercase, hyphen-separated, derived from the task title (e.g., `CLARIFY-user-preferences.md`)
- If a file with the same name exists, ask the user whether to overwrite or rename.

### YAML Frontmatter

Every clarify document **must** include this frontmatter so `/develop` can validate readiness:

```yaml
---
status: clarified        # draft → clarified → implemented
title: [Task Title]
created: [ISO 8601 date]
clarified: [ISO 8601 date]
---
```

- During the clarification process (before user confirms completeness), use `status: draft`.
- Only set `status: clarified` after the user reviews and approves the final output.

### Document Template

```markdown
---
status: clarified
title: [Task Title]
created: [ISO 8601 date]
clarified: [ISO 8601 date]
---

# Task Clarification: [Task Title]

**Source**: [brief description of original user input]

## Summary

[2–3 sentence summary of the clarified task]

## Assumptions & Open Items

- [Any unresolved assumptions, flagged explicitly]
- [Any items deferred or explicitly excluded]

## Requirements

| ID | Type | Description | Acceptance Criteria | Priority | Source |
|---|---|---|---|---|---|
| REQ-001 | Functional | … | … | Must | … |

## Epics

> _Omit this section if task is small enough for a flat backlog._

| ID | Title | Description | Requirements |
|---|---|---|---|
| EPIC-001 | … | … | REQ-001, REQ-002 |

## Product Backlog

| ID | Epic | Title | User Story | Acceptance Criteria | Priority | Size | Dependencies | Assumptions |
|---|---|---|---|---|---|---|---|---|
| PBI-001 | EPIC-001 | … | As a …, I want …, so that … | - [ ] … | Must | M | — | — |

## Dependency Map

[Optional: Mermaid diagram showing PBI dependencies if complex]

## Priority Stack Validation

- **Security**: ✅/❌ [brief note]
- **Correctness**: ✅/❌ [brief note]
- **Clarity**: ✅/❌ [brief note]
- **Simplicity**: ✅/❌ [brief note]
- **Performance**: ✅/❌ [brief note]
```

Present the artifact to the user for final review. Only set `status: clarified` after user approval.

---

## Integration with `/develop`

Once the document has `status: clarified`, the user can run `/develop` (optionally with the file path or specific EPIC/PBI IDs) to implement the backlog using the repository's existing skills and AGENTS.md guidance.
