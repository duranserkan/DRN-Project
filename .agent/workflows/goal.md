---
description: Pursue a user goal through the fastest safe workflow route using DiSCOS, AGENTS.md, TRIZ, and Priority/Quality Stack gates
---

> **Goal Mode**: finish the user's real objective by the shallowest safe route.
> Sources: `AGENTS.md` for portable rules, `.agent/rules/DiSCOS.md` for TRIZ/security/context discipline, `./_shared/workflow-operating-model.md` for mutation/evidence/verification/subagent gates, `.agent/skills/basic-agentic-development/SKILL.md` for autonomy/context economy, and routed workflows for task-specific gates.
> Do not duplicate source-owned rules here.
> **Estimated context: ~2.2K tokens** + routed workflow/skills.

## 1. Start

Run the shared Startup Gate and Repository Extension Gate once, load only the selected route workflow/skills, cache conclusions, and stop when the next decision has enough evidence. Use the shared Workflow Composition Contract when chaining routes. Delegate route-specific detail to routed workflows; do not inline `/search`, `/clarify`, `/answer`, `/develop`, `/review`, `/optimize`, `/test`, `/documentation`, or profile/custom route rules here.

## 2. Contract

Use this state mentally for trivial work or visibly for non-trivial/resumable work:

```markdown
Goal=[full outcome, not current progress]
Done=[observable criteria + proof needed]
Class=[trivial | standard | significant | critical]
Route=[direct | /search | /clarify -> /answer -> /develop | /review | /optimize | /test | /documentation | profile/custom route chain]
Flags=[auto(default when omitted) | cad | ro | profile/custom flag when declared]
Approval=[none | direct scoped request | workflow-tolerated | explicit approval required/recorded]
Assumptions=[accepted | rejected | blocking]
Delegation=[none | subagent scope + reason | unavailable]
Decision=[IFR, contradiction, TRIZ resolution or Priority Stack fallback]
Iteration=[1..n full Evidence -> Execution -> Audit cycles; final report prints n and a one-line summary per cycle]
State=[files touched, verification, blockers, budget if constrained, CAD artifact paths/statuses when Flags=cad]
```

Use active tracker if any. Create goals only when requested. Complete only after audit proof; block only under the platform's strict blocker rule.

## 3. Classify And Route

Use AGENTS/DiSCOS Priority Stack and shared operating model at classification, mutation, and completion. Defaults: `trivial`/`standard` -> act when scoped and reversible; `significant` -> plan, then proceed only when explicitly requested and approval-tolerable; `critical` -> discuss first and require explicit approval. Treat unclear risk as `significant`.

A direct user request to change a named file/scope permits reversible `trivial`/`standard` edits and approval-tolerable `significant` edits inside that scope. Approval-tolerable means bounded, reversible, auditable, non-security work with no auth, secrets, privacy, tenant, data-loss, schema/migration, public-contract, production, infrastructure, CI/CD, dependency, VCS, destructive, failed-gate, unclear-gate, unresolved-input, unverified-assumption, or temp-artifact lifecycle risk. For approval-tolerable work, `/goal` may produce `ApprovalRecord=workflow-tolerated` for workflow-local confirmation or approval gates that accept it by recording the Priority Stack decision and planned verification before mutation; final completion still requires executed verification evidence. Stop and record `Approval=explicit approval required` when the approval cannot be satisfied by that record.

Routes: `direct` answer/simple safe edit; `/search` missing context then return; `/clarify` ambiguous requirements or acceptance; `/answer` incorporate answers/assumptions/approval; `/develop` implementation-heavy; `/review` diffs/docs/workflows/skills/artifacts; `/optimize` agent content quality/routing/token use; `/test` tests only; `/documentation` docs/release-note drift; profile/custom workflows when the repository profile, skill index, task, or discovered convention selects a narrower local route. Routed workflows own their gates, artifacts, and output format.

Flags are route presets:
- `auto`: when no flag is supplied, choose the shallowest safe route from current evidence after applying profile/custom workflow and skill overlays. Run `/search` only when context is missing, ambiguity affects the route, or risk is `significant`/`critical`; then use enriched evidence to choose `cad`, `ro`, or a custom normal `/goal` route and record why.
- `cad`: first run `/search`, then run CAD through routed gates (`/clarify auto` -> `/answer auto` -> `/develop`) with any profile/custom clarify, answer, develop, review, optimize, documentation, or test overlays that match the task. `/goal` may produce `ApprovalRecord=workflow-tolerated` to satisfy `/answer auto` confirmation and execute `/develop` without another stop only when the route is approval-tolerable, the accepting workflow gates allow that record, the `DEVELOP-*` source/status/staleness checks pass, no unverified assumptions remain, `needs_review=false`, and the `/goal` Decision records a Priority Stack pass plus planned verification for the scoped work. This route must create and preserve `.agent/temp/CLARIFY-*.md` and `.agent/temp/DEVELOP-*.md` unless it stops before the owning workflow can create them; if either artifact is missing, do not claim CAD completion. For workflow/skill improvements, CAD may compose `/review` and `/optimize` as quality gates without owning their rules. Stop when any routed workflow or custom overlay has a failed or unclear gate, unresolved user input, security-sensitive work, or approval that cannot be satisfied by an accepted shared approval record. Before completion or stop, report `CAD proof=[workflow files loaded; profile/custom overlays used or skipped; CLARIFY artifact path + status; DEVELOP artifact path + status; /review or /optimize proof when used]` after resolving current files from `.agent/workflows/` and `.agent/temp/`.
- `ro`: run `/search` to choose the smallest defensible scope after applying profile/custom review or optimize overlays, then `/review -> /optimize`; feed review findings into optimization and apply only after approval records exact scope, candidate set, and severity. Re-run `/review` after moderate/significant workflow or skill edits; final verdict must be ✅ or ⚠️.

Prefer direct `/goal` for clear `trivial`/`standard` work, but still apply matching profile/custom skill overlays before answering or editing. Use `/clarify -> /answer -> /develop` only for durable requirements, backlog, approvals, or handoff. If evidence changes class, route, or selected custom overlay, record why and switch.

## 4. Orchestration Gates

Before route actions, apply the shared TRIZ Decision Record and Subagent Gate. Record `Decision` and `Delegation` for non-trivial work; if useful subagents are unavailable, record `Delegation=unavailable` and proceed by the smallest safe solo route.

## 5. Loop

Frame -> Discover -> Decide with TRIZ/Priority Stack -> Execute routed action or approved change -> Verify with fresh allowed evidence -> Review via Quality Stack -> Continue/report.

For `trivial` or `standard` goals, complete after one full iteration when the objective is proven, no routed workflow requires more, and another iteration would not add evidence or reduce risk. For `significant`, `critical`, resumable, or ambiguity-heavy goals, run up to 3 full iterations unless blocked or waiting on required human input, and stop earlier only when fresh audit evidence proves no further iteration would improve the outcome. An iteration is one complete cycle of all three phases below; do not count the phases as separate iterations. Each iteration feeds its audit output into the next iteration's evidence input:
1. **Evidence**: objective, sources, counterexamples, route, risk, proof.
2. **Execution**: shared TRIZ Decision Record -> route action, smallest approved change, or handoff.
3. **Audit**: requirement proof, regression, docs, skills, release notes, lessons.

Track iteration summaries while working:
```markdown
Iteration 1: Evidence=[...] Execution=[...] Audit=[...]
Iteration 2: Evidence=[...] Execution=[...] Audit=[...]
Iteration 3: Evidence=[...] Execution=[...] Audit=[...]
```

After required iterations, continue only while another iteration adds evidence, reduces ambiguity, or improves the artifact. Stop discovery when the next decision has enough evidence.

## 6. Completion

Audit every requirement from the objective and referenced artifacts against authoritative current evidence: diff, command output, runtime/rendered result, explicit approval, or task-appropriate static proof. Treat weak, indirect, missing, or memory-based evidence as incomplete.

Completion gate: higher Priority Stack gates block lower ones: Security (no exposure), Correctness (objective proven), Clarity (standalone), Simplicity (no avoidable ceremony), Performance (cost justified).

## 7. Verification And Report

1. Run `git diff --check` after code, docs, workflow, or skill edits unless blocked.
2. Never restore, build, run apps, or run tests in `/goal`; route/ask and report `not run per repo rule`.
3. Decide docs, skills, release notes, and lessons using `AGENTS.md`, the profile, and routed workflows; report `not required` when no trigger applies.
4. For workflow/skill optimization, report before/after token estimate when practical (`chars / 4`).
5. On resume, answer the newest user message first and restate the contract only when useful.

Final report: outcome, `Iteration count: n` with one-line Evidence/Execution/Audit summaries, changes/findings, verification, CAD proof when `Flags=cad`, docs/release/lesson decision, residual risk, next action.
