---
description: Clarify a user task into testable requirements, epics, and backlog items through iterative questioning. Use DiSCOS, AGENTS.md, repository profile, and loaded skills.
---

> **Pipeline**: `/clarify` (1/3) -> `/answer` -> `/develop` · [Status Lifecycle](./_shared/status-lifecycle.md) · [Operating Model](./_shared/workflow-operating-model.md)
> **Estimated context: ~2.4K tokens**

## 1. Mandate

Act as Technical Business Analyst. Bridge business intent and technical architecture. Research first. Resolve ambiguity. Produce a testable, self-contained `.agent/temp/CLARIFY-*` artifact.

Run Startup Gate once: read `AGENTS.md`, `.agent/rules/DiSCOS.md` when present, `.agent/repository-profile.md` when present, this workflow, and only needed skills. Reuse loaded conclusions unless sources changed.

Invariants:
- Never bypass CAD. Always create or update the resolved `CLARIFY-*` artifact. Do not implement. Hand off through `/answer`, then `/develop`.
- Ask or route when scope is unclear, confidence is below threshold, a security-sensitive decision appears, destructive/VCS action is needed, a gate fails, source evidence is stale, or `[ASSUMPTION - unverified]` remains.
- Accept a non-critical assumption only when allowed; record source and mitigation.

## 2. Resolve Input And Artifact

1. Resolve loop input before deriving the slug:
   - One existing `CLARIFY-*` path: continue that lineage, set `previous_artifact`, and treat remaining text as new raw input.
   - Multiple paths: ask which lineage to continue.
   - Missing/unreadable path: stop.
   - Prior artifact with no new text: continue from its summary, open items, review/optimization findings, and latest instruction.
2. Record raw input verbatim under `## Raw Input`; for loops also record `Previous artifact:`.
3. Create `.agent/temp/CLARIFY-[task-slug].md` unless the user specifies a path. For loop collisions, append `-iteration-[N]`.
4. If the target exists and no loop-safe suffix applies, ask to overwrite or rename. Edit in place only on explicit request and after review gates.
5. Set `status: draft`; set `clarifying` when question round 1 starts.
6. For lineage, set `iteration`, `previous_artifact`, `previous_status`, `previous_updated`, and `previous_sha256`.
7. When supplied or unambiguously discovered, set `previous_develop_artifact`, `previous_develop_sha256`, `previous_walkthrough_artifact`, and `previous_commit`. Apply shared lineage evidence rules.
8. When waiting on the user, preserve current `status` and set `blocked_on_user: true`.

## 3. Research And Risk

Research before questions; spend at most 20% of task time by default. For stale, current, or security-sensitive correctness gaps, `/search` or scoped research may extend automatically up to 40%. Beyond 40% requires explicit approval; otherwise stop and record not searched.

- Prefer `/search`; fallback to scoped file search, the skill index, required skills, and required external references.
- Append evidence to `## Enrichment Context` under `Codebase Findings`, `Knowledge Items`, `Relevant Skills`, `External References`, and `Initial Observations`.
- In `Initial Observations`, capture scope, Security/Privacy triggers, compliance/data/performance/design implications, lifecycle/approval implications, specialist-lens triggers, and assumptions. Cite evidence or tag `[ASSUMPTION - unverified]`.

For loop input, add `### Enriched Lineage Snapshot` so `/answer` can produce a current `DEVELOP-*` handoff without reopening old artifacts by default. Summarize; do not paste full artifacts or large diffs.

| Part | Required Evidence |
|---|---|
| Prior clarify | Path, status, time, SHA-256, intent, summary, answers, requirements, PBIs, risks, assumptions, open items. |
| Prior develop | Path, source metadata, status, summary, context, architecture, constraints, risk register, verification, approval, stale/needs-review flags. |
| Prior implementation | Walkthrough or commit/ref, files changed, implemented PBIs/REQs, verification, deviations, follow-ups, approved status changes. |
| Delta | New change, carried or superseded decisions, changed criteria, new risks, `/answer` and `/develop` notes. |

Lineage rules:
- Prefer supplied `DEVELOP-*`, `WALKTHROUGH-*`, and commit refs. If missing, discover matching `DEVELOP-*` files whose `source` names the prior `CLARIFY-*`; ask if multiple exist.
- Apply shared lineage evidence rules. Record or ask on missing, ambiguous, or conflicting evidence.
- Use read-only git inspection. Infer commits only from explicit, unambiguous user references.
- Summarize supplied `/review` or `/optimize` findings; route to those workflows only under their gates.
- If implementation evidence conflicts with clarification intent, record the conflict and ask unless the user clearly superseded it.

## 4. Analyze And Question

Silently apply Five Whys, MECE, Inversion, Second-Order Thinking, and TRIZ/IFR.

Before each question round, run the shared [Expert Lens Pass](./_shared/workflow-operating-model.md#expert-lens-pass) after current research and analysis.

- Always include Security/Privacy. Add product, business, domain, UX, infrastructure, data, compliance, performance, or verification lenses only when evidence supports them.
- List selected lenses once per round with concise rationale.
- Ask for referenced files/URLs, state assumptions, and request validation.
- Give rationale only when value, security, correctness, usability, compliance, performance, or implementation risk is not obvious.
- Label expert-lens questions; leave other questions unlabeled. Preserve priority order.

For each answer round:
1. Integrate answers into the artifact.
2. Re-run analysis, risk, scope, and lenses when evidence changed.
3. Ask the next round if gaps remain.
4. If no gaps remain, summarize and ask: `Is this complete and accurate?`
5. Stop the question loop on confirmation.

Limit to 3 rounds. If gaps remain, keep `status: clarifying`, set `blocked_on_user: true`, tag unknowns `[ASSUMPTION - unverified]`, and do not produce `draft-self-reviewed`.

`ApprovalRecord=workflow-tolerated` may satisfy completeness confirmation only when an allowed producer such as `/goal cad` supplies it, the route is approval-tolerable, and no gaps remain.

## 5. Decompose Deliverables

| Output | Rule |
|---|---|
| Requirements | Use `REQ-NNN`; include Type, Description, Acceptance Criteria, Priority (MoSCoW), Source. |
| Epics | Use `EPIC-NNN`; include Title, Description, linked REQs. Skip when backlog is flat: <=4 PBIs and one value area. |
| PBIs | Use `PBI-NNN`; include Epic, Title, User Story, Acceptance Criteria, Priority, Size, Dependencies, Context, Assumptions. |
| INVEST | Every PBI must be Independent, Negotiable, Valuable, Estimable, Small, and Testable. |
| Assumptions | `[ASSUMPTION - unverified]` blocks `draft-self-reviewed`. Retag accepted non-critical assumptions as `[ASSUMPTION - accepted]` and carry them to `/answer` risk register. |
| Traceability | Record selected lenses in Q&A. Carry useful findings into answer rationale, sources, criteria, guidance, risks, and Priority Stack Validation. Do not add raw expert transcripts. |

## 6. Quality Gate And Artifact

Evaluate top-down; higher gate failures block lower gates.

| Gate | Pass Question |
|---|---|
| Security | Are security/compliance captured? Do data-sensitive PBIs include security criteria? |
| Correctness | Do REQs trace to user needs? Are criteria testable and unambiguous? |
| Clarity | Can someone outside this conversation understand it? |
| Simplicity | Is decomposition necessary and minimal? |
| Performance | Are performance/scalability captured where relevant? |

Confirm expert-lens findings are traceable where they changed requirements, risks, constraints, criteria, or question rationale.

Run `/review` on the resolved `CLARIFY-*`. If gates pass, INVEST passes, no `[ASSUMPTION - unverified]` remains, no scope creep exists, and no Critical findings remain, set `status: draft-self-reviewed`.

Minimum skeleton:

```markdown
---
status: draft # draft -> clarifying -> draft-self-reviewed; /answer owns clarified
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
```

Add sections in this order: `Raw Input`, `Enrichment Context` with required subheadings, `Clarification Q&A`, `Summary`, `Assumptions & Open Items`, `Discovery & Guidance`, `Requirements`, `Epics` when needed, `Product Backlog`, `Dependency Map` when needed, and `Priority Stack Validation`.

Use the Section 5 columns for REQs, EPICs, and PBIs. Under `Clarification Q&A`, record selected lenses, questions, and answers by round. Under `Priority Stack Validation`, cover Security, Correctness, Clarity, Simplicity, and Performance.

## 7. Handoff

Present the `draft-self-reviewed` artifact path and route to `/answer`. `/clarify` never mutates target files.

| Mode | Action |
|---|---|
| Default/manual | Stop after presenting the path. Tell the user to run `/answer` with it. |
| `/clarify auto` | Invoke `/answer auto` on the new artifact only when autonomy gates allow it. |
| Approved skip | `/answer` may skip approval only with explicit confirmation or valid `ApprovalRecord=workflow-tolerated`, no `[ASSUMPTION - unverified]`, and all approval criteria satisfied. It still must produce `DEVELOP-*` before `/develop`. |

For loop input, state the current artifact, lineage evidence, and explicit branch point under the shared supersession rule. `/clarify` does not create branches or commits; record VCS intent as guidance for `/develop` or `/commit-polish`.
