---
description: Clarify a user task into testable requirements, epics, and backlog items through iterative questioning. Use DiSCOS, AGENTS.md, repository profile, and loaded skills.
---

> **Pipeline**: `/clarify` (1/3) -> `/answer` -> `/develop` · [Status Lifecycle](./_shared/status-lifecycle.md) · [Operating Model](./_shared/workflow-operating-model.md)
> **Estimated context: ~2.9K tokens**

---

## 1. Mandate

Act as Technical Business Analyst. Bridge business intent and technical architecture. Research first. Resolve ambiguity. Produce testable requirements and a self-contained `CLARIFY-*` artifact.

Run the shared Startup Gate before work: read `AGENTS.md`, `.agent/rules/DiSCOS.md` when present, `.agent/repository-profile.md` when present, this workflow, and only needed skills.

### Invariants

- Reuse startup/profile conclusions in the same session; re-read only changed or missing sources.
- Accept a non-critical assumption only when allowed; document source and mitigation.
- Stop and ask, or route to the owning workflow, when scope is unclear, confidence is below threshold, security-sensitive choice appears, destructive/VCS action is needed, a gate fails, a source is stale, or `[ASSUMPTION - unverified]` remains.
- Never bypass CAD. Always create or update the resolved `.agent/temp/CLARIFY-*` artifact. Do not implement directly. Progress through `/answer` and `/develop`.

---

## 2. Resolve Input And Artifact

1. Resolve loop input before deriving the slug:
   - One existing `.agent/temp/CLARIFY-*.md` path: continue that lineage, set `previous_artifact`, and treat remaining text as new raw input.
   - Multiple `CLARIFY-*` paths: ask which lineage to continue.
   - Missing or unreadable supplied path: stop.
   - No new text after a prior artifact: continue from its summary, open items, review/optimization findings, and latest user instruction.
2. Record raw input verbatim under `## Raw Input`. For loop input, include new text and `Previous artifact:`.
3. Create `.agent/temp/CLARIFY-[task-slug].md` unless the user specifies a path. For loop input, use `.agent/temp/CLARIFY-[task-slug]-iteration-[N].md` when the base filename exists.
4. If the target exists and no loop-safe suffix is available, ask to overwrite or rename. Edit an existing artifact in place only by explicit user request and after normal review gates.
5. Set `status: draft`; advance to `clarifying` when the first question round starts.
6. For loop input, set `iteration`, `previous_artifact`, `previous_status`, `previous_updated`, and `previous_sha256`.
7. If supplied or unambiguously discoverable, set `previous_develop_artifact`, `previous_develop_sha256`, `previous_walkthrough_artifact`, and `previous_commit`. Apply the shared lineage evidence rule.
8. If waiting on the user, keep the current `status` and set `blocked_on_user: true` until the answer is recorded.

---

## 3. Research And Risk

Research before questions. Spend at most 20% of task time.

- Prefer `/search` for codebase, knowledge items, skills, and web context; append results to `## Enrichment Context`.
- Fallback: search files, `.agent/skills/overview-skill-index/SKILL.md`, and required external references.
- Use these subheadings: `Codebase Findings`, `Knowledge Items`, `Relevant Skills`, `External References`, `Initial Observations`.
- In `Initial Observations`, capture scope boundaries, Security/Privacy triggers, compliance/data/performance/design implications, lifecycle/approval implications, specialist-lens triggers, and assumptions. Cite evidence or tag `[ASSUMPTION - unverified]`.

For loop input, add `### Enriched Lineage Snapshot`. It must let `/answer` produce a current `DEVELOP-*` handoff without reopening old artifacts by default. Summarize evidence; do not paste full artifacts or large diffs.

| Part | Evidence |
|---|---|
| Prior clarify | Path, status, time, SHA-256, raw intent, summary, answers, requirements, PBIs, risks, assumptions, open items. |
| Prior develop | Path, source metadata, status, summary, context, architecture, constraints, risk register, verification, approval, stale/needs-review flags. |
| Prior implementation | Walkthrough or commit/ref, changed files, implemented PBIs/REQs, verification, deviations, follow-ups, approved status changes. |
| Delta | New change, carried decisions, superseded decisions, changed acceptance criteria, new risks, `/answer` and `/develop` notes. |

Evidence rules:

- Prefer supplied `DEVELOP-*`, `WALKTHROUGH-*`, and commit refs. If absent, discover matching `DEVELOP-*` files whose `source` points to the previous `CLARIFY-*`; ask if multiple exist.
- Apply shared lineage evidence rules. Record or ask on missing, ambiguous, or conflicting evidence.
- Use read-only git inspection. Infer a previous commit only when the user explicitly refers to it and the commit is unambiguous; otherwise record the evidence gap.
- Summarize supplied `/review` or `/optimize` findings. Route to those workflows only under their gates; do not copy their rules or mutate prior temp artifacts by default.
- If implementation evidence conflicts with clarification intent, record and ask unless the user supplied a clear supersession decision.

---

## 4. Analyze

Silently apply:

- Five Whys for root purpose.
- MECE for complete decomposition.
- Inversion for failure modes.
- Second-Order Thinking for downstream effects.
- TRIZ/IFR for maximum benefit with zero harm.

---

## 5. Ask Clarification Questions

Before each question round, run the shared [Expert Lens Pass](./_shared/workflow-operating-model.md#expert-lens-pass) after research and analysis are current. Assign lenses from evidence, not raw input alone.

- Include Security/Privacy plus evidence-backed product, business, domain, UX, infrastructure, or other task lenses.
- List selected lenses once per round with concise reasoning.
- Ask for referenced files/URLs. State assumptions and ask for validation.
- Give rationale only when value, security, correctness, usability, compliance, performance, or implementation risk is not obvious.
- Label questions asked by an expert lens; leave other questions unlabeled. Preserve priority order.

---

## 6. Iterate To Clarity

For each answer round:

1. Integrate answers into the artifact.
2. Re-run first-principles analysis on the updated state.
3. Refresh risk and scope. Reuse lenses unless the risk profile changed; if changed, reassign through Section 5.
4. If gaps remain, ask the next round.
5. If no gaps remain, summarize and ask: `Is this complete and accurate?`
6. Stop on user confirmation.

`ApprovalRecord=workflow-tolerated` may satisfy completeness confirmation only when an allowed producer such as `/goal cad` supplies it, the route is approval-tolerable, and no gaps remain.

Limit to 3 rounds. If gaps remain after round 3, tag unknowns `[ASSUMPTION - unverified]`, keep `status: clarifying`, set `blocked_on_user: true`, and do not produce `draft-self-reviewed`.

---

## 7. Decompose Deliverables

| Output | Rule |
|---|---|
| Requirements | Use `REQ-NNN`; include Type, Description, Acceptance Criteria, Priority (MoSCoW), Source. |
| Epics | Use `EPIC-NNN`; include Title, Description, linked REQs. Skip when backlog is flat (<=4 PBIs, one value area). |
| PBIs | Use `PBI-NNN`; include Epic, Title, User Story, Acceptance Criteria, Priority, Size (S/M/L/XL), Dependencies, Context, Assumptions. |
| INVEST | Every PBI must be Independent, Negotiable, Valuable, Estimable, Small, and Testable. |
| Assumptions | `[ASSUMPTION - unverified]` blocks `draft-self-reviewed`. Retag accepted non-critical assumptions as `[ASSUMPTION - accepted]` and carry them to `/answer` risk register. |
| Traceability | Record selected lenses in Q&A. Carry useful findings into answer rationale, sources, acceptance criteria, Discovery & Guidance, risks, and Priority Stack Validation. Do not add a raw expert transcript. |

---

## 8. Quality Gate

Evaluate top-down. A higher gate failure blocks lower gates.

| Gate | Pass Question |
|---|---|
| Security | Are security/compliance captured? Do data-sensitive PBIs include security criteria? |
| Correctness | Do REQs trace to user needs? Are acceptance criteria testable and unambiguous? |
| Clarity | Can someone outside this conversation understand it? |
| Simplicity | Is decomposition necessary and minimal? |
| Performance | Are performance/scalability captured where relevant? |

Confirm Expert Lens findings are traceable where they changed requirements, risks, constraints, acceptance criteria, or question rationale.

---

## 9. Write And Review Artifact

Verify Quality Gate and INVEST. Run `/review` on the resolved `.agent/temp/CLARIFY-*` artifact. If no red Critical findings remain, set `status: draft-self-reviewed`.

### Pre-Presentation Checklist

- [ ] Priority Stack gates pass.
- [ ] INVEST passes for all PBIs.
- [ ] No unresolved `[ASSUMPTION - unverified]`.
- [ ] No scope creep.
- [ ] `/review` passed with no red Critical findings.

### Skeleton

```markdown
---
status: draft # Section 5 -> clarifying; Section 9 -> draft-self-reviewed; never set clarified here
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

**Source**: [original input summary]

## Raw Input
[User input verbatim]

## Enrichment Context
### Codebase Findings
### Knowledge Items
### Relevant Skills
### External References
### Enriched Lineage Snapshot
> _Only for continued `CLARIFY-*` lineage._
#### Previous Clarify Snapshot
#### Previous Develop Snapshot
#### Previous Implementation Snapshot
#### Iteration Delta
### Initial Observations

## Clarification Q&A
### Round 1
**Selected Expert Lenses:** [lens: reason]
**Questions:** 1. [optional lens label] [question]
**Answers** _(by: user | /answer)_: 1. [optional lens label] [answer]

## Summary
[2-3 sentence summary]

## Assumptions & Open Items
[accepted assumptions, unresolved questions, exclusions]

## Discovery & Guidance
- **Context/Files**: [2-4 paths]
- **Architecture**: [boundaries]
- **Risks**: [edge cases]

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

Present the `draft-self-reviewed` artifact path and route to `/answer`. `/clarify` never authorizes target-file mutation.

| Mode | Action |
|---|---|
| Default/manual | Stop after presenting the path. Tell the user to run `/answer` with it. |
| `/clarify auto` | Invoke `/answer auto` on the new artifact only when autonomy gates allow it. |
| Approved skip | `/answer` may skip approval only with explicit confirmation or valid `ApprovalRecord=workflow-tolerated`, no `[ASSUMPTION - unverified]`, and all approval criteria satisfied. It still must produce `DEVELOP-*` before `/develop`. |

For loop input, state the current artifact, lineage evidence, and explicit branch point under the shared supersession rule. `/clarify` does not create branches or commits; record VCS intent as guidance for `/develop` or `/commit-polish`.
