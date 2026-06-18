---
description: Shared operating model for agent workflows
---

> Load with task workflows that mutate or review agent-consumed documents.

## Startup Gate

1. Read `AGENTS.md`.
2. Read `.agent/rules/DiSCOS.md` when present.
3. Read `.agent/repository-profile.md` when present.
4. Read the target workflow and only the skills needed for the task.

Reuse context already loaded in the same session. Do not re-read broad skill sets when a focused skill or workflow is enough.

## Repository Extension Gate

After the Startup Gate, resolve repository-specific workflow and skill overlays before choosing a generic fallback:

1. Use `.agent/repository-profile.md` as the source of truth for custom workflow routes, skill load sets, framework overlays, frontend conventions, test commands, release rules, and documentation modules.
2. Load only the profile-declared custom workflow or skill that matches the task, route, layer, or flag; skip unrelated custom guidance even when it exists.
3. If the profile is silent, discover by convention from `.agent/workflows/<route>.md`, `.agent/workflows/load-skills-<group>.md`, `.agent/skills/<group>-*/SKILL.md`, and `.agent/skills/overview-skill-index/SKILL.md`.
4. Preserve the strictest gate from every composed source. Custom routes may refine routing, accuracy, completeness, and clarity, but must not weaken security, approval, lifecycle, mutation, or verification gates. A named approval record may satisfy a workflow-local approval gate only when this shared model, the status lifecycle, the producing workflow, and the accepting gate owner all allow that record for the same bounded scope.
5. If a profile-declared custom workflow or skill is missing, record the gap, fall back to the smallest safe generic route when possible, and report the missing repository extension in the audit.

## Expert Lens Pass

Use this shared pass when `/clarify`, `/answer`, or another workflow asks for expert-lens review. It is a concise challenge mechanism, not a theatrical roleplay or approval body.

### Ordering

1. Keep the Security/Privacy gate active from intake onward.
2. Assign specialist lenses only after Startup Gate context, raw task review, initial risk/scope check, and context enrichment. Do not preselect specialist lenses solely from the raw prompt when repository context could change the risk profile.
3. For follow-up passes, integrate the latest answers or artifact changes first. Refresh lens selection only when the risk profile changed.

### Lens Selection

Select `2-4` full lenses for each pass:

- Include at least one Product, Business, ROI, Growth, or MVP lens.
- Keep Security/Privacy mandatory even when it is not one of the full lenses.
- Promote Security/Privacy to a full lens when the task touches authentication, authorization, user input, sensitive data, browser protections, secrets, dependencies, CI/CD, infrastructure, external communication, or storage. When promoted, it counts toward the `2-4` limit and takes precedence over optional specialist lenses.
- If more than four lenses are relevant, choose by risk and value using TRIZ first, then the Priority Stack. Merge adjacent concerns when useful, such as Security+Compliance, Database+Performance, or Architecture+Operations, without hiding a higher-priority risk.

Conditionally add these specialist lenses when relevant:

| Lens | Trigger |
|---|---|
| Compliance/Audit | Regulated data, privacy, retention, legal, payment, identity, enterprise, or contractual requirements |
| Frontend UX/Accessibility | User-facing UI, forms, workflows, navigation, feedback states, or design behavior |
| Database/Performance | Persistence, queries, migrations, reporting, scale, concurrency, indexing, or latency |
| Architecture/Maintainability | Cross-layer design, boundaries, public APIs, or framework conventions |
| Test/QA | Acceptance criteria, regression risk, observability, or verification strategy |
| Operations/SRE | Deployment, reliability, runtime behavior, rollback, or supportability |
| Domain SME/User Workflow | Domain rules, user roles, operational realities, edge cases, manual workarounds, or how the work is actually performed |
| Integration/API Contract | External APIs, events, webhooks, public contracts, backward compatibility, SDKs, or cross-system interoperability |
| Data Governance/Analytics | Data quality, lineage, metric definitions, reporting correctness, retention, auditability, or analytics assumptions |
| Developer Experience/Public API | Framework or package work, naming, discoverability, ergonomics, migration impact, consumer-facing API clarity, or documentation needs |
| Content/Localization | User-facing copy, terminology, internationalization/localization, support content, or accessibility-adjacent language concerns |
| AI/Automation Safety | LLM features, agent workflows, generated content, prompt injection, evaluation, human review, or failure containment |

### Pass Rules

1. Lenses challenge the work; they do not approve it. User/TPO authority remains final. A lens cannot grant approval, clear status flags, bypass review, satisfy lifecycle gates, or weaken Security/Privacy.
2. No lens may invent facts. Every claim must cite user input, repository context, loaded skill/workflow, source file, external reference, or be tagged `[ASSUMPTION - unverified]`.
3. Resolve conflicts with TRIZ first, then the Priority Stack: Security, Correctness, Clarity, Simplicity, Performance.
4. Keep output concise. Record synthesized findings, not raw simulated transcripts. Avoid theatrical roleplay, fake consensus, approval language, or persona labels unless a label makes a question or tradeoff clearer.
5. Preserve traceability through question rationale, answer tradeoffs, requirement sources, acceptance criteria, risk register entries, Priority Stack validation notes, and implementation constraints. Do not require a permanent "Expert Lens Transcript" section.

## Workflow Composition Contract

Compose workflows by ownership, not copied rules:

| Workflow | Owns | When composed |
|---|---|---|
| `/goal` | Route choice, iteration, completion audit | Delegates route details and reports proof from composed workflows |
| `/clarify` -> `/answer` -> `/develop` | CAD requirements, approval, handoff, implementation status | May reference `/review` or `/optimize` as quality gates without inlining their rules |
| `/review` | Read-only findings, verdicts, transition recommendations | Caller owns any mutation, status update, or optimization apply step |
| `/optimize` | Preview, apply approval, content optimization, metrics | Uses `/review` before or after moderate/significant workflow or skill edits |
| Profile/custom workflows | Repository-specific routing, domain gates, local skill overlays | Compose when the profile, skill index, task, route, or discovered convention selects them |
| `_shared/*` | Startup, mutation, evidence, lifecycle, verification primitives | Referenced by workflows to prevent duplicate gate text |

Rules:
1. Preserve the strictest chained gate: preview-first, approval, read-only, and lifecycle gates never weaken by composition. Use only approval records defined by the shared status lifecycle, produced by an allowed workflow, and accepted by the owning workflow.
2. Pass summaries, findings, candidate sets, metrics, status transitions, or artifact paths forward; do not paste full source-owned checklists downstream.
3. Use section links or file references for another workflow's rules. Duplicate only the phrase needed to make the local step executable.
4. `/review` supplies evidence and recommendations; `/optimize` owns preview/apply and metrics; `/review` validates the result when required.
5. Repository-specific custom workflows and skills are overlays, not replacements for shared gates, unless `AGENTS.md` or the repository profile explicitly declares a stricter local owner.
6. CAD artifacts record routed workflows and approval state for workflow/skill changes; they do not own `/review` or `/optimize` gates.

## Priority Queue

Classify work before acting:

| Priority | Meaning | Action |
|---|---|---|
| `P0` | Security, correctness, data loss, irreversible mutation, broken lifecycle | Fix first; block handoff until resolved |
| `P1` | Ambiguity, portability drift, missing evidence, maintainability risk | Fix in the current pass unless explicitly scoped out |
| `P2` | Polish, deduplication, token efficiency, minor references | Fix after P0/P1 when low risk |

When priorities conflict, use TRIZ first: seek a source-owned, root-cause option that satisfies both constraints. If a real tradeoff remains, apply the Priority Stack: Security, Correctness, Clarity, Simplicity, Performance.

## TRIZ Decision Record

For non-trivial conflicts or friction, record:
- **IFR**: ideal final result with maximum benefit and minimum harm.
- **Contradiction**: competing constraints or false tradeoff.
- **Resolution**: no-tradeoff/source-owned fix, or Priority Stack fallback.
- **Residual risk**: required only when a workaround is the safest acceptable route.

Prefer permanent acceptable fixes over workaround-only patches. Do not make permanence absolute when the safest path is temporary, reversible, and explicitly risk-managed.

## Subagent Gate

Use subagents when available for independent codebase questions, disjoint write scopes, parallel verification, or second-look review. Skip delegation when work is sequential, transfer cost exceeds value, scopes overlap, or the active workflow requires the main agent to own the gate.

Subagents support workflows; they do not replace them. Give bounded scope, read-only/disjoint write ownership, no VCS/destructive authority, and an evidence summary. Verify against current files or commands before relying on subagent output.

## Mutation Tiers

| Tier | Examples | Rule |
|---|---|---|
| Read-only | Research, review, search, preview, dry run | Never edit files or state |
| Reversible docs | Workflow, skill, README, report edits | Edit only scoped files; run `git diff --check` |
| State mutation | Status flags, temp plan progress, generated handoff docs | Allowed only when the owning workflow says so |
| VCS mutation | Branch creation, commits, rewrites, tags, push | Requires explicit user approval unless the user directly requested that exact action |
| Destructive | Delete, reset, force push, data migration | Requires explicit approval and a rollback plan |

Do not rely on "git diff is rollback" as approval for unrequested edits. Preview-first workflows must not mutate until the apply step is confirmed.

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

Every review finding or workflow gate failure must include:

- **Evidence**: file/path and line, command output, or source link.
- **Impact**: what breaks, leaks, confuses, or slows.
- **Invariant**: which rule, status contract, or priority gate is violated.
- **Recommendation**: concrete next action.
- **Confidence**: high, medium, low.
- **Verification**: run, not run per rule, blocked, or not applicable.

Findings are caused by changed lines or changed behavior, but reviewers may inspect unchanged context needed to prove or disprove impact.

## Verification

After workflow, skill, or documentation edits:

1. Check scoped diffs against the requested plan.
2. Run `git diff --check` unless blocked.
3. Run any additional static checks listed by the active workflow.
4. Report build/test commands as "not run per repo rule" when the repository profile or user did not allow them.
