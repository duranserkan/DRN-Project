---
description: Pursue a user goal through the fastest safe route using DiSCOS, AGENTS.md, TRIZ, and Priority/Quality Stack gates
---

> **Goal Mode**: finish the user's real objective by the shallowest safe route.
> Sources: `AGENTS.md`, `.agent/rules/DiSCOS.md`, `./_shared/workflow-operating-model.md`, `.agent/skills/basic-agentic-development/SKILL.md`, and routed workflows.
> Do not duplicate source-owned rules.
> **Estimated context: ~2.2K tokens** plus routed workflow/skills.

## 1. Start

Run the shared Startup Gate and Repository Extension Gate once. Load only the selected route workflow and skills. Cache conclusions. Stop discovery when the next decision has enough evidence.

Use the shared Workflow Composition Contract when chaining routes. Delegate task-specific gates to routed workflows; do not inline `/search`, `/clarify`, `/answer`, `/develop`, `/review`, `/optimize`, `/test`, `/documentation`, or profile/custom route rules.

## 2. Contract

Use this state mentally for trivial work and visibly for non-trivial or resumable work:

```markdown
Goal=[full outcome]
Done=[observable criteria + proof]
Class=[trivial | standard | significant | critical]
Route=[direct | /search | /clarify -> /answer -> /develop | /review | /optimize | /test | /documentation | profile/custom chain]
Flags=[auto | cad | ro | profile/custom]
Approval=[none | direct scoped request | workflow-tolerated | explicit required/recorded]
Assumptions=[accepted | rejected | blocking]
Delegation=[none | scope + reason | unavailable]
Decision=[IFR, contradiction, TRIZ resolution or Priority Stack fallback]
Iteration=[1..n Evidence -> Execution -> Audit cycles]
State=[files, verification, blockers, budget, CAD artifact paths/status]
```

Use the active tracker when present. Create platform goals only when requested. Complete only after audit proof. Mark blocked only under the platform's strict blocker rule.

## 3. Classify

Apply AGENTS.md, DiSCOS, and the shared operating model at classification, mutation, and completion.

| Class | Default action |
|---|---|
| `trivial` | Act directly when scoped and reversible. |
| `standard` | Act directly when scoped and reversible. |
| `significant` | Plan, then proceed only when explicitly requested and approval-tolerable. |
| `critical` | Discuss first and require explicit approval. |

Treat unclear risk as `significant`.

A direct request to change a named file or scope permits reversible `trivial`/`standard` edits and approval-tolerable `significant` edits inside that scope.

`Approval-tolerable` means bounded, reversible, auditable, non-security work with no auth, secrets, privacy, tenant, data-loss, schema/migration, public-contract, production, infrastructure, CI/CD, dependency, VCS, destructive, failed-gate, unclear-gate, unresolved-input, unverified-assumption, or temp-artifact lifecycle risk.

For approval-tolerable work, `/goal` may produce `ApprovalRecord=workflow-tolerated` only when the accepting workflow allows it. Record the Priority Stack decision and planned verification before mutation. Final completion still requires task-appropriate verification evidence under the shared Command Execution Authorization Gate. Stop with `Approval=explicit required` when the record cannot satisfy the gate.

## 4. Route

Choose the shallowest safe route.

| Route | Use when |
|---|---|
| `direct` | Safe answer or simple scoped edit. |
| `/search` | Context is missing; return after evidence. |
| `/clarify` | Requirements or acceptance are ambiguous. |
| `/answer` | Answers, assumptions, or approval must update CAD artifacts. |
| `/develop` | Implementation-heavy work. |
| `/review` | Diffs, docs, workflows, skills, or artifacts need read-only findings. |
| `/optimize` | Agent content needs correctness-per-token, routing, or quality improvement. |
| `/test` | Tests only. |
| `/documentation` | Module docs or release-note drift. |
| profile/custom | Profile, skill index, task, or convention selects a narrower route. |

Routed workflows own their gates, artifacts, and output format.

### Flags

`auto`: Default when omitted. Apply profile/custom overlays, then choose the shallowest safe route. Run `/search` only when context is missing, ambiguity affects routing, or risk is `significant`/`critical`; then record whether evidence selects `cad`, `ro`, or a custom route.

`cad`: Run `/search`, then `/clarify auto -> /answer auto -> /develop`. Apply matching profile/custom clarify, answer, develop, review, optimize, documentation, and test overlays. `/goal` may supply `ApprovalRecord=workflow-tolerated` to satisfy `/answer auto` and continue into `/develop` only when:

- The route is approval-tolerable.
- The accepting workflow allows the record.
- `DEVELOP-*` source, status, and staleness checks pass.
- No unverified assumptions remain.
- `needs_review=false`.
- The `/goal` Decision records a Priority Stack pass and planned verification.

Create and preserve `.agent/temp/CLARIFY-*.md` and `.agent/temp/DEVELOP-*.md` unless the route stops before their owning workflow can create them. Do not claim CAD completion if either required artifact is missing. For workflow or skill improvements, CAD may compose `/review` and `/optimize` as quality gates; it does not own their rules.

Stop when any routed workflow or custom overlay has a failed or unclear gate, unresolved user input, security-sensitive work, or approval that cannot be satisfied by an accepted shared record. Before completion or stop, resolve current files from `.agent/workflows/` and `.agent/temp/`, then report `CAD proof=[workflow files loaded; overlays used/skipped; CLARIFY path/status; DEVELOP path/status; /review or /optimize proof]`.

`ro`: Run `/search` to choose the smallest defensible scope. Apply profile/custom review or optimize overlays, then run `/review -> /optimize`. Feed review findings into optimization. Apply only after approval records exact scope, candidates, and severity. Re-run `/review` after moderate or significant workflow/skill edits. Final verdict must be pass or warning.

Prefer direct `/goal` for clear `trivial` or `standard` work, with matching overlays. Use CAD only for durable requirements, backlog, approvals, or handoff. If evidence changes class, route, or overlay, record why and switch.

## 5. Orchestrate

Before route actions, apply the shared TRIZ Decision Record and Subagent Gate. Record `Decision` and `Delegation` for non-trivial work. If useful subagents are unavailable, record `Delegation=unavailable` and proceed by the smallest safe solo route.

Loop:

1. **Evidence**: objective, sources, counterexamples, route, risk, proof.
2. **Execution**: TRIZ Decision Record -> route action, approved change, or handoff.
3. **Audit**: requirement proof, regression check, docs, skills, release notes, lessons.

Run one full loop for proven `trivial` or `standard` goals when no routed workflow requires more and another loop would not add evidence or reduce risk. Run up to three loops for `significant`, `critical`, resumable, or ambiguity-heavy goals unless blocked or waiting on required human input. Feed each audit into the next evidence pass. Continue only while another loop improves evidence, ambiguity, or artifact quality.

Track summaries while working:

```markdown
Iteration 1: Evidence=[...] Execution=[...] Audit=[...]
Iteration 2: Evidence=[...] Execution=[...] Audit=[...]
Iteration 3: Evidence=[...] Execution=[...] Audit=[...]
```

## 6. Complete

Audit every requirement from the user objective and referenced artifacts against current evidence: diff, allowed command output, allowed runtime/rendered result, explicit approval, or task-appropriate static proof. Treat weak, indirect, missing, or memory-based evidence as incomplete.

Completion gate follows the Priority Stack:

1. Security: no exposure.
2. Correctness: objective proven.
3. Clarity: result stands alone.
4. Simplicity: no avoidable ceremony.
5. Performance: cost justified.

## 7. Verify And Report

1. Run `git diff --check` after code, docs, workflow, or skill edits unless blocked.
2. Never restore, build, run apps, or run tests in `/goal`; command catalogs are reference-only, so route/ask and report `not run per repo rule`.
3. Decide docs, skills, release notes, and lessons using AGENTS.md, the profile, and routed workflows; report `not required` when no trigger applies.
4. For workflow or skill optimization, report before/after token estimate when practical (`chars / 4`).
5. On resume, answer the newest user message first. Restate the contract only when useful.

Final report: outcome, `Iteration count: n` with one-line Evidence/Execution/Audit summaries, changes/findings, verification, CAD proof when `Flags=cad`, docs/release/lesson decision, residual risk, next action.
