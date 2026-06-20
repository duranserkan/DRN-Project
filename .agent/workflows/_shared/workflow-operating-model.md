---
description: Shared operating model for agent workflows
---

## Startup Gate

When present read once per task, then reuse conclusions:

1. `AGENTS.md`.
2. `.agent/rules/DiSCOS.md`
3. `.agent/repository-profile.md`
4. The target workflow and only task-needed skills.

Avoid broad reloads when a focused workflow, profile entry, or skill is enough.

## Repository Extension Gate

Resolve repository overlays before generic fallback:

1. `.agent/repository-profile.md` owns custom routes, skill load sets, framework/frontend overlays, commands, release rules, and docs modules.
2. Load only profile-declared workflows/skills matching the task, route, layer, or flag.
3. If profile is silent, discover from `.agent/workflows/<route>.md`, `load-skills-<group>.md`, `.agent/skills/<group>-*/SKILL.md`, and `overview-skill-index`.
4. Preserve the strictest security, approval, lifecycle, mutation, and verification gate. Custom routes may refine routing, accuracy, completeness, or clarity only upward.
5. Approval records satisfy local gates only when this model, status lifecycle, producer, and accepting owner allow the same bounded scope.
6. If an overlay is missing, record it, use the smallest safe generic route when possible, and report the gap in the audit.

## Expert Lens Pass

Use when `/clarify`, `/answer`, or another workflow asks for expert-lens review. This is a concise challenge pass, not roleplay or approval.

1. Select specialist lenses only after Startup context, raw task review, initial risk/scope check, and enrichment.
2. Include Security/Privacy lens per priority stack.
3. When applicable include at least one
  - Product, Business, ROI, Growth, or MVP lenses.
  - Domain expert lenses.
  - Necessary task relevant lenses such as UI/UX, Infrastructure.
4. For follow-up passes, integrate new answers or artifact changes first; refresh lenses if needed.

### Pass Rules

1. Lenses challenge; they never approve, clear flags, bypass review, satisfy lifecycle gates, or weaken Security/Privacy. User/TPO authority remains final.
2. Claims cite user input, repo context, loaded skill/workflow, source file, or external reference; otherwise tag `[ASSUMPTION - unverified]`.
3. Resolve conflicts with TRIZ first, then Priority Stack: Security, Correctness, Clarity, Simplicity, Performance.
4. Record synthesized findings, not transcripts. Avoid roleplay, fake consensus, approval language, and persona labels unless they clarify a tradeoff.
5. Carry findings into question rationale, answer tradeoffs, sources, acceptance criteria, risks, Priority Stack notes, and implementation constraints. No permanent transcript section required.

## Workflow Composition Contract

Compose workflows by ownership, not copied rules:

| Workflow | Owns | Compose Rule |
|---|---|---|
| `/goal` | Route choice, iteration, completion audit | Delegate route details; report proof. |
| `/clarify` -> `/answer` -> `/develop` | CAD requirements, approval, handoff, implementation status | May reference `/review` or `/optimize`; do not inline them. |
| `/review` | Read-only findings, verdicts, transition recommendations | Caller owns mutation, status, and apply steps. |
| `/optimize` | Preview, apply approval, content optimization, metrics | Use `/review` before/after moderate or significant workflow/skill edits. |
| Profile/custom | Repository routing, domain gates, local overlays | Compose when profile, skill index, task, route, or convention selects them. |
| `_shared/*` | Startup, mutation, evidence, lifecycle, verification primitives | Reference to prevent duplicated gates. |

Rules:
1. Preserve the strictest chained gate: preview-first, approval, read-only, and lifecycle gates never weaken.
2. Use only approval records defined by the status lifecycle, produced by an allowed workflow, and accepted by the owning workflow.
3. CAD chain invariant: when `/clarify` is invoked for work that will mutate files, the route must continue through `/answer`, a `DEVELOP-*` artifact, and `/develop`. Automation may bridge stages only when autonomy and approval gates allow; manual handoff waits for the next workflow, never direct implementation.
4. Pass summaries, findings, candidates, metrics, status transitions, or artifact paths forward; do not paste full source-owned checklists downstream.
5. Link to another workflow's rules; duplicate only phrases needed for local executability.
6. `/review` supplies evidence/recommendations; `/optimize` owns preview/apply/metrics; `/review` validates when required.
7. Profile/custom workflows and skills overlay shared gates; they do not replace them unless `AGENTS.md` or the profile names a stricter local owner.
8. CAD artifacts record routed workflows and approval state for workflow/skill changes; they do not own `/review` or `/optimize` gates.

## Non-Compressible Gates

The CAD pipeline must never shorten away or skip required security/privacy checks, source metadata, status transitions, approval records, assumption tags, mutation tiers, stale detection, review gates, or verification evidence. Specific checklists and templates reside inside their respective workflow files to keep them actionable and in the active context.

## Priority Queue

Classify work before acting:

| Priority | Meaning | Action |
|---|---|---|
| `P0` | Security, correctness, data loss, irreversible mutation, broken lifecycle | Fix first; block handoff until resolved |
| `P1` | Ambiguity, portability drift, missing evidence, maintainability risk | Fix in the current pass unless explicitly scoped out |
| `P2` | Polish, deduplication, token efficiency, minor references | Fix after P0/P1 when low risk |

When priorities conflict, use TRIZ first: seek a source-owned, root-cause option that satisfies both constraints. If a real tradeoff remains, apply the Priority Stack: Security, Correctness, Clarity, Simplicity, Performance.

## TRIZ Decision Record

For non-trivial conflict or friction, record `IFR`, `Contradiction`, `Resolution`, and `Residual risk` when a workaround is the safest acceptable route.

Prefer permanent acceptable fixes over workaround-only patches. Do not make permanence absolute when the safest path is temporary, reversible, and explicitly risk-managed.

## Subagent Gate

Use subagents for independent codebase questions, disjoint write scopes, parallel verification, or second-look review. Skip when work is sequential, transfer cost exceeds value, scopes overlap, or the active workflow requires main-agent ownership.

Subagents support workflows; they do not replace them. Give bounded scope, read-only/disjoint write ownership, no VCS/destructive authority, and an evidence summary. Verify against current files or commands before relying on subagent output.

## Mutation Tiers

| Tier | Examples | Rule |
|---|---|---|
| Read-only | Research, review, search, preview, dry run | Never edit files or state |
| Reversible docs | Workflow, skill, README, report edits | Edit only scoped files; run `git diff --check` |
| State mutation | Status flags, temp plan progress, generated handoff docs | Allowed only when the owning workflow says so |
| VCS mutation | Branch creation, commits, rewrites, tags, push | Requires explicit user approval unless the user directly requested that exact action |
| Destructive | Delete, reset, force push, data migration | Requires explicit approval and a rollback plan |

Do not treat `git diff` as approval for unrequested edits. Preview-first workflows must not mutate until apply is confirmed.

## Portable Tool Verbs

Use capability names in reusable workflows. Map them to the active platform's tools at execution time.

| Need | Portable wording |
|---|---|
| Read a known file | Read file |
| Search text or identifiers | Search text |
| Find paths by name or glob | Find files |
| Inspect code structure | Inspect outline/symbols |
| Read agent knowledge resources | List/read knowledge resources |
| Search external sources | Web search or read URL |

Prefer repository facts and source files over external references. Use web sources only when internal sources are insufficient or the user requests current outside information.

## Evidence Contract

Every review finding or workflow gate failure includes: `Evidence` (file/path+line, command output, or source link), `Impact`, violated `Invariant`, concrete `Recommendation`, `Confidence` (high/medium/low), and `Verification` (run, not run per rule, blocked, or not applicable).

Findings are caused by changed lines or changed behavior, but reviewers may inspect unchanged context needed to prove or disprove impact.

## Verification

After workflow, skill, or documentation edits:

1. Check scoped diffs against the requested plan.
2. Run `git diff --check` unless blocked.
3. Run any additional static checks listed by the active workflow.
4. Report build/test commands as "not run per repo rule" when the repository profile or user did not allow them.