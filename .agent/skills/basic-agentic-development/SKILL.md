---
name: basic-agentic-development
description: Use when planning, implementing, refactoring, or verifying work under repository autonomy and execution rules.
last-updated: 2026-11-14
difficulty: basic
---

# Agentic Development

## Silent Partner Protocol

Act autonomously on routine work. Ask when ambiguity blocks progress or decisions are irreversible.

**Autonomy Ladder**:
1. **Trivial** (formatting, imports, lint): Act immediately
2. **Standard** (new files, tests, refactors): Act, then report
3. **Significant** (architecture, schema, API breaking): Plan, seek approval, then act
4. **Critical** (security, data migration, production): Prohibited under the sync safety contract. For non-sync tasks, provide purpose, full content, exact commands, and risks before seeking explicit approval. Ad-hoc helpers follow the same gate under Execution.

These tiers do not override workflow, command, or VCS gates.

### Scrutiny & Audit Posture

- Support actions, reasoning, tool use, and plans with reviewable evidence.
- Verify assumptions against source or tag them `[ASSUMPTION - unverified]`.
- Support completion with static checks or authorized execution evidence.

---

## Context Economy

| Principle | Action |
|-----------|--------|
| Thin loading by default | Use the current workflow and scoped loader. Reserve `/load-skills-all` for explicit broad context or `/update all` agent-configuration self-sync |
| Summarize, don't echo | Summarize findings and cite relevant files instead of repeating file contents |
| Batch reads | Read related files together |
| Early exit | Stop reading when you have enough context |
| Prefer outlines | Inspect outline/symbols before full file reads when the platform supports it |
| Cache mentally | Don't re-read files already in context |

---

## Development Loop

### 1. Discovery

- Read `AGENTS.md`, DiSCOS, the repository profile, the workflow, then relevant skills
- Inspect outline/symbols to understand structure before diving in when supported
- Search text for exact identifiers and find files by name or glob before broad reads
- Stop when evidence is sufficient. Seek counterexamples before concluding.

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
- **No Unapproved Helper Scripts**: Ad-hoc helper scripts or code are prohibited under the sync safety contract. For non-sync tasks, first provide purpose, full content, exact commands, and risks. Obtain explicit approval before writing or executing them.

#### Refactoring for Clarity

- Name meaningful compound or repeated conditions and coherent operations, especially in UI rendering. Keep simple checks inline. Use ordinary branches when nested expressions obscure intent.
- Use cyclomatic complexity to identify code worth reviewing. Simplify redundant decisions and separate coherent responsibilities, but do not extract helpers solely to lower a score.
- Put decisions on the owning model or module. Use properties for cheap state checks and methods for arguments or generated values. Resolve and validate generated content together to avoid duplicate calls.
- Preserve short-circuiting, null handling, evaluation count, captured state, callback identity, and mutation/disposal order. Move helpers outside closures only when they do not need local state.
- Check analyzer suggestions against framework contracts and UI semantics. Follow the [diagnostic suppression guidance](../basic-code-review/SKILL.md#diagnostic-suppressions).

#### Allocations And Stable Lookups

- Distinguish startup, lazy first-use, and steady-state costs. Inspect dependency implementations before attributing allocations. Move stable initialization outside repeated callbacks.
- Avoid temporary lookup keys. Use spans for string slicing and comparison when supported. Retain original strings when downstream APIs need them. Prefer direct loops when they remove verified closure or enumeration allocations without obscuring intent.
- Cache reusable read-only views and lookup adapters. Use an immutable snapshot when later configuration changes must not affect active readers. Keep configuration builders and response headers mutable.
- Consider an immutable index for data built infrequently and queried repeatedly. Group entries by lookup key so requests visit only matching rules. Keep a direct comparison for a single entry. Do not assume an index wins for small collections or invent a crossover threshold without measurements.
- In .NET, `FrozenDictionary` can support these indexes. For span queries, cache a compatible `GetAlternateLookup<ReadOnlySpan<char>>()` adapter instead of calling `ToString()` per lookup. Verify target framework and comparer support.
- Preserve comparer semantics, duplicate-key policy, order within groups, snapshot isolation, and lazy initialization. Cache stable inputs without caching a custom provider's potentially changing answer. Verify hits, misses, case variants, duplicates, multiple values per key, and changes after snapshot creation.
- An async callback that completes synchronously may not allocate. Preserve callback order, exceptions, and cancellation. Do not remove async solely because it appears expensive.

When measurement is authorized, warm up first and keep setup, assertions, and allocating substitutes outside the measured section. Separate lookup costs from provider, middleware, and response-header storage costs. Removing an allocation in source does not prove zero allocation for a whole request or improved throughput. Report unexecuted measurements explicitly.

### 4. Verification

**Mandatory order when permitted**: Build → Test → Lint → Self-Correct
- Resolve commands from the profile, manifests, or CI. Use the destination's test runner and package manager.
- Follow `AGENTS.md` command gates. Without execution permission, use static verification and report skipped commands.
- Fix warnings introduced by your changes (Boy Scout Rule)
- Use relative paths in code/configs, not absolute paths

**Self-Correction Loop**:
- ✅ Fail → Read error → Fix → Re-verify → Success
- ⚠️ After **2 failed attempts** → Stop. Report to user: what you tried, what failed, hypotheses, recommended next steps

### 5. Lessons Learned

Follow `AGENTS.md` Lessons Learned for the profile-resolved file, entry format, and deduplication. Capture reusable insights only. Move durable rules to their owning guidance and remove stale lessons during cleanup.

---

## Anti-Patterns

| Anti-Pattern | Correct Approach |
|-------------|-----------------|
| Ignoring test failures to move forward | Fix failures before proceeding |
| Over-engineering simple requests | Match complexity to problem (Gall's Law) |
| Skipping security considerations | Always check against security checklist |
| Creating placeholder implementations | Deliver complete, runnable code |
| Assuming names or namespaces | Verify against source, project metadata, `GlobalUsings.cs`, or symbol search |
| Importing uninstalled libraries | Check package manifests before use. Ask before adding a dependency |
| Choosing runtime-access mechanisms without repository context | Follow the repository profile's runtime-access conventions |
| Weakening production invariants for tests | Follow the repository profile's environment conventions and use explicit test configuration |
| Using obsolete or deprecated code | Use active framework APIs. Do not introduce obsolete types or members, or suppress deprecation diagnostics with `#pragma` or `[SuppressMessage]` |

---

## Autonomy Guide

Apply the Silent Partner Protocol and active workflow gates. Reading and routine refactoring may proceed. Ask before destructive work, security changes, or unresolved architectural decisions. Execution and VCS actions retain their separate approval gates.

---

## Verification Checklist

Report checks actually performed and any unverified build, test, or warning status. Confirm local patterns, security, public documentation impact, and reusable lessons.

---

## Portable Tool Selection Guide

Use capability names from the [operating model](../../workflows/_shared/workflow-operating-model.md#portable-tool-verbs). Read known files, search exact identifiers, and discover paths before broad exploration.

---

## Related Skills

- [basic-security-checklist.md](../basic-security-checklist/SKILL.md) - Security standards
- [basic-code-review.md](../basic-code-review/SKILL.md) - Review standards (Priority Stack)
- [overview-skill-index.md](../overview-skill-index/SKILL.md) - Skill discovery catalog
