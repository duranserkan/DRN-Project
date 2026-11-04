---
name: basic-agentic-development
description: "Agentic development standards - Silent Partner Protocol, context economy, development loop (discovery, planning, execution, verification), and anti-patterns for efficient autonomous development. Keywords: agentic, ai-agent, development-loop, context-economy, autonomous, discovery, planning, execution, verification, anti-patterns, silent-partner"
last-updated: 2026-11-04
difficulty: basic
tokens: ~1.5K
---

# Agentic Development

## Silent Partner Protocol

Act autonomously on routine work. Ask when ambiguity blocks progress or decisions are irreversible.

**Autonomy Ladder**:
1. **Trivial** (formatting, imports, lint): Act immediately
2. **Standard** (new files, tests, refactors): Act, then report
3. **Significant** (architecture, schema, API breaking): Plan, seek approval, then act
4. **Critical** (security, data migration, production, ad-hoc helper script writing or execution): Prohibited under the sync safety contract; for non-sync tasks, always provide a detailed description (purpose, full content, exact commands, potential risks) and obtain explicit user approval first

### Scrutiny & Audit Posture

- Support actions, reasoning, tool use, and plans with reviewable evidence.
- Verify assumptions against source or tag them `[ASSUMPTION - unverified]`.
- Support completion with static checks or authorized execution evidence.

---

## Context Economy

| Principle | Action |
|-----------|--------|
| Thin loading by default | Use existing source-owned guidance with the current workflow route and scoped loaders (`/load-skills-basic`, `/load-skills-drn`, `/load-skills-test`, `/load-skills-frontend`); reserve `/load-skills-all` for explicit broad context or `/update all` agent-configuration self-sync |
| Summarize, don't echo | Summarize findings and cite relevant files instead of repeating file contents |
| Batch reads | Read related files together |
| Early exit | Stop reading when you have enough context |
| Prefer outlines | Inspect outline/symbols before full file reads when the platform supports it |
| Cache mentally | Don't re-read files already in context |

---

## Development Loop

### 1. Discovery

- Read `AGENTS.md` → workflow → relevant skills
- Inspect outline/symbols to understand structure before diving in when supported
- Search text for exact identifiers and find files by name or glob before broad reads
- **Stop when sufficient** — don't read every file

### 2. Planning

- Define scope and constraints
- Identify affected files and dependencies
- Consider security implications (Priority Stack)
- For significant changes: write plan, seek approval
- Design and optimize for pit of success
- Omit restore/build/run/test/benchmark/load-test steps from plans unless explicitly allowed.

### 3. Execution

- Work incrementally: smallest testable unit first
- Keep changes unstaged for review by default. Do not stage changes, commit, or amend commits unless the user explicitly authorizes the action. Preserve any pre-existing staged changes.
- Validate after each change (build, lint, test) only when the active instructions allow those commands
- Follow existing patterns in the codebase
- Avoid unnecessary allocations and repeated work. Simplify algorithms and choose data structures that fit access patterns, ordering, and lifetime needs. Use source evidence and explicit assumptions to justify changes. Measure uncertain tradeoffs, preserve correctness, and avoid complexity whose benefit is unproven.
- Follow the repository profile's dependency-injection and configuration conventions
- **No Unapproved Helper Scripts**: Do NOT create, write, or execute ad-hoc helper scripts or code (e.g. Python, Bash, Node, custom tools). Ad-hoc scripts are strictly prohibited under the sync safety contract. For non-sync tasks, writing or executing an ad-hoc script requires a prior detailed description (purpose, full content, exact commands, potential risks) and explicit user approval before writing or running it.

#### Refactoring for Clarity

- Name meaningful compound or repeated conditions and coherent operations, especially in UI rendering. Keep simple checks inline. Use ordinary branches when nested expressions obscure intent.
- Use cyclomatic complexity to identify code worth reviewing. Simplify redundant decisions and separate coherent responsibilities, but do not extract helpers solely to lower a score.
- Put decisions on the owning model or module. Use properties for cheap state checks and methods for arguments or generated values. Resolve and validate generated content together to avoid duplicate calls.
- Preserve short-circuiting, null handling, evaluation count, captured state, callback identity, and mutation/disposal order. Move helpers outside closures only when they do not need local state.
- Check analyzer suggestions against framework contracts and UI semantics. Follow the [diagnostic suppression guidance](../basic-code-review/SKILL.md#diagnostic-suppressions).

### 4. Verification

**Mandatory order when permitted**: Build → Test → Lint → Self-Correct
- Resolve commands from the profile, manifests, or CI. Use the destination's test runner and package manager.
- Under `AGENTS.md`, do not build or run tests unless explicitly allowed; use static verification and report skipped commands when permission is absent.
- Fix warnings introduced by your changes (Boy Scout Rule)
- Use relative paths in code/configs, not absolute paths

**Self-Correction Loop**:
- ✅ Fail → Read error → Fix → Re-verify → Success
- ⚠️ After **2 failed attempts** → Stop. Report to user: what you tried, what failed, hypotheses, recommended next steps

### 5. Lessons Learned

Capture only durable, generalizable insights discovered during the task into `AGENTS.LessonsLearned.md` (repo root - create if missing).

**When**: reusable mistake, anti-pattern, non-obvious insight, correction, or pattern that can change future decisions across cases.

**Exclude**: one-time findings, incident history, or case details that cannot be generalized. Move durable rules into the owning docs, skills, workflows, or source comments and remove stale lesson entries during cleanup.

**Entry format**: `## N. Descriptive Title` -> `### Case`, `### General Rule`, `### Decision Boundary`, and `### Source To Update`. Keep the case specific enough to recognize the failure mode, but make the rule portable enough to apply beyond that case.

**Before adding**: read existing entries - update rather than duplicate.

---

## Anti-Patterns

| Anti-Pattern | Correct Approach |
|-------------|-----------------|
| Ignoring test failures to move forward | Fix failures before proceeding |
| Over-engineering simple requests | Match complexity to problem (Gall's Law) |
| Skipping security considerations | Always check against security checklist |
| Creating placeholder implementations | Deliver complete, runnable code |
| Assuming names or namespaces | Verify against source, project metadata, `GlobalUsings.cs`, or symbol search |
| Importing uninstalled libraries | Check `*.csproj`/`package.json` before using; ask user before adding |
| Choosing runtime-access mechanisms without repository context | Follow the repository profile's runtime-access conventions |
| Weakening production invariants for tests | Follow the repository profile's environment conventions and use explicit test configuration |
| Using obsolete or deprecated code | Never introduce deprecated or obsolete APIs, types, interfaces, or members (e.g. `[Obsolete]`, `ASPDEPR*`); always use modern, active framework primitives and never suppress deprecation diagnostics with `#pragma` or `[SuppressMessage]` |

---

## Autonomy Guide

| Situation | Action |
|-----------|--------|
| Destructive/irreversible | Ask first |
| Security-related | Ask first |
| Reading/analysis | Proceed |
| Staging, committing, or amending | Require explicit user authorization |
| Testing your own work | Proceed only when current instructions allow build/test commands |
| Ambiguous architecture | Ask first |
| Ad-hoc helper scripts | Follow the prohibition and approval requirements under Execution |
| Routine refactoring following patterns | Proceed |

---

## Verification Checklist

- [ ] Code compiles without errors, or build was not run because current instructions forbid it
- [ ] Tests pass, or tests were not run because current instructions forbid them
- [ ] No new warnings introduced
- [ ] Follows existing codebase patterns
- [ ] Security implications considered
- [ ] Documentation updated (if needed)
- [ ] Lessons captured (if a non-obvious insight was discovered)

---

## Portable Tool Selection Guide

Use capability names in generic skills. Map them to the active platform tools at execution time.

| Need | Portable capability |
|------|------|
| File exists, known location | Read file |
| Understand file structure | Inspect outline/symbols when supported, then read focused sections |
| Exact text search | Search text |
| Find files by name/pattern | Find files |
| Explore directory | List directory |

---

## Related Skills

- [basic-security-checklist.md](../basic-security-checklist/SKILL.md) - Security standards
- [basic-code-review.md](../basic-code-review/SKILL.md) - Review standards (Priority Stack)
- [overview-skill-index.md](../overview-skill-index/SKILL.md) - Skill discovery catalog
