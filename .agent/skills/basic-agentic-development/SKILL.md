---
name: basic-agentic-development
description: Agentic development standards - Silent Partner Protocol, context economy, development loop (discovery, planning, execution, verification), and anti-patterns for efficient autonomous development. Keywords: agentic, ai-agent, development-loop, context-economy, autonomous, discovery, planning, execution, verification, anti-patterns, silent-partner
last-updated: 2026-02-15
difficulty: basic
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
| Load only what's needed | Use `/basic`, `/drn`, `/test`, `/frontend` — not `/all` — for focused tasks |
| Summarize, don't echo | Never repeat file contents in conversation |
| Batch reads | Read related files together |
| Early exit | Stop reading when you have enough context |
| Prefer outlines | Use `view_file_outline` before full file reads |
| Cache mentally | Don't re-read files already in context |

---

## Development Loop

### 1. Discovery
- Read `AGENTS.md` → workflow → relevant skills
- Use `view_file_outline` to understand structure before diving in
- Search with `grep_search` (exact) or `find_by_name` (glob)
- **Stop when sufficient** — don't read every file

### 2. Planning
- Define scope and constraints
- Identify affected files and dependencies
- Consider security implications (Priority Stack)
- For significant changes: write plan, seek approval

### 3. Execution
- Work incrementally: smallest testable unit first
- Validate after each change (build, lint, test)
- Follow existing patterns in the codebase
- Use attribute-based DI (`[Scoped<T>]`, `[Singleton<T>]`)

### 4. Verification

**Mandatory order**: Build → Test → Lint → Self-Correct
- .NET: `dotnet build` → `dotnet test`
- Node: `npm run build` / `npm run typecheck` → `npm test`
- Fix warnings introduced by your changes (Boy Scout Rule)
- Use relative paths in code/configs, not absolute paths

**Self-Correction Loop**:
- ✅ Fail → Read error → Fix → Re-verify → Success
- ⚠️ After **2 failed attempts** → Stop. Report to user: what you tried, what failed, hypotheses, recommended next steps

---

## Anti-Patterns

| Anti-Pattern | Correct Approach |
|-------------|-----------------|
| Reading all skills when only one area is relevant | Load specific workflow (`/drn`, `/test`, etc.) |
| Echoing file contents back in conversation | Summarize key points, reference file paths |
| Asking for permission on trivial changes | Act, then report |
| Implementing without reading existing code | Discovery first |
| Ignoring test failures to move forward | Fix failures before proceeding |
| Over-engineering simple requests | Match complexity to problem (Gall's Law) |
| Skipping security considerations | Always check against security checklist |
| Creating placeholder implementations | Deliver complete, runnable code |
| Using namespaces/names from assumption | Verify with `grep_search`, `view_file_outline`, check `GlobalUsings.cs` |
| Importing uninstalled libraries | Check `*.csproj`/`package.json` before using; ask user before adding |

> **Name & Namespace Hallucination**: Never assume a namespace from folder structure. Always verify exact name with `view_file_outline` or `grep_search`. Example: `DRN.Framework.EntityFramework.Models` doesn't exist — the correct namespace is `DRN.Framework.EntityFramework.Context`.

---

## Autonomy Guide

| Situation | Action |
|-----------|--------|
| Destructive/irreversible | Ask first |
| Security-related | Ask first |
| Reading/analysis | Proceed |
| Testing your own work | Proceed |
| Ambiguous architecture | Ask first |
| Routine refactoring following patterns | Proceed |

---

## Verification Checklist

- [ ] Code compiles without errors
- [ ] Tests pass
- [ ] No new warnings introduced
- [ ] Follows existing codebase patterns
- [ ] Security implications considered
- [ ] Documentation updated (if needed)

---

## Tool Selection Guide

| Need | Tool |
|------|------|
| File exists, known location | `view_file` |
| Understand file structure | `view_file_outline` first, then `view_code_item` |
| Exact text search | `grep_search` |
| Find files by name/pattern | `find_by_name` |
| Explore directory | `list_dir` |

---

## Related Skills

- [basic-security-checklist.md](../basic-security-checklist/SKILL.md) - Security standards
- [basic-code-review.md](../basic-code-review/SKILL.md) - Review standards (Priority Stack)
- [overview-skill-index.md](../overview-skill-index/SKILL.md) - Skill discovery catalog