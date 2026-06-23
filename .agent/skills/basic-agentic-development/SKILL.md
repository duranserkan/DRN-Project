---
name: basic-agentic-development
description: "Agentic development standards - Silent Partner Protocol, context economy, development loop (discovery, planning, execution, verification), and anti-patterns for efficient autonomous development. Keywords: agentic, ai-agent, development-loop, context-economy, autonomous, discovery, planning, execution, verification, anti-patterns, silent-partner"
last-updated: 2026-06-23
difficulty: basic
tokens: ~1.5K
---

# Agentic Development

> Standards for efficient, autonomous AI-assisted development.

## Silent Partner Protocol

The agent operates as a **silent partner** — it works autonomously, does not ask for permission on routine operations, and only interrupts with questions when genuinely blocked on ambiguity or facing irreversible decisions.

**Autonomy Ladder**:
1. **Trivial** (formatting, imports, lint): Act immediately
2. **Standard** (new files, tests, refactors): Act, then report
3. **Significant** (architecture, schema, API breaking): Plan, seek approval, then act
4. **Critical** (security, data migration, production): Always discuss first

---

## Context Economy

Context is a **finite resource**. Every token consumed reduces remaining capacity.

| Principle | Action |
|-----------|--------|
| Thin loading by default | Use existing source-owned guidance with the current workflow route and scoped loaders (`/load-skills-basic`, `/load-skills-drn`, `/load-skills-test`, `/load-skills-frontend`); reserve `/load-skills-all` for explicit broad context or sync workflows |
| Summarize, don't echo | Never repeat file contents in conversation |
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

### 3. Execution

- Work incrementally: smallest testable unit first
- Validate after each change (build, lint, test) only when the active instructions allow those commands
- Follow existing patterns in the codebase
- Follow the repository profile's dependency-injection and configuration conventions

### 4. Verification

**Mandatory order when permitted**: Build → Test → Lint → Self-Correct
- .NET: `dotnet build` → `dotnet run --project` (for test projects)
- Node: `npm run build` / `npm run typecheck` → `npm test`
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
| Reading all skills when only one area is relevant | Load the repository profile and the specific workflow or skill family for the task. |
| Echoing file contents back in conversation | Summarize key points, reference file paths |
| Asking for permission on trivial changes | Act, then report |
| Implementing without reading existing code | Discovery first |
| Ignoring test failures to move forward | Fix failures before proceeding |
| Over-engineering simple requests | Match complexity to problem (Gall's Law) |
| Skipping security considerations | Always check against security checklist |
| Creating placeholder implementations | Deliver complete, runnable code |
| Using namespaces/names from assumption | Verify with text search, outline/symbol inspection when supported, and `GlobalUsings.cs` |
| Importing uninstalled libraries | Check `*.csproj`/`package.json` before using; ask user before adding |

> **Name & Namespace Hallucination**: Never assume a namespace from folder structure. Always verify exact names with source files, project metadata, or search before using them.

---

## Autonomy Guide

| Situation | Action |
|-----------|--------|
| Destructive/irreversible | Ask first |
| Security-related | Ask first |
| Reading/analysis | Proceed |
| Testing your own work | Proceed only when current instructions allow build/test commands |
| Ambiguous architecture | Ask first |
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
