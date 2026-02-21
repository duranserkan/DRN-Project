---
description: Optimize agent-consumed content using DiSCOS and AGENTS.md (skills, workflows, docs, reports, todos) — reduce waste, enhance effectiveness, maximize context efficiency
---

> **Trigger**: `/optimize <scope>` or `/optimize` (prompts for scope).
>
> **Mission**: Maximize context efficiency — two equal vectors: (1) **Reduce** waste (filler, redundancy, verbosity), (2) **Enhance** effectiveness (clarity, completeness, correctness of instructions). Token count may increase when enhancement yields better outcomes.
>
> **Principle**: DiSCOS Context Management — Preserve: Conclusions > Decisions > Patterns > Instances.
>
> **TRIZ Contradiction**: Brevity vs. Completeness. Resolution: optimize for **correct output per token**, not token count alone. When reducing content forces compensating actions (extra tool calls, follow-up questions, agent errors), the reduction is counter-productive. Reject false tradeoffs — satisfy both constraints.

---

## 1. Resolve Scope

- **File/directory path** → Target that file or all `.md` files in that directory.
- **Content-type keyword** → Resolve to file set:

| Keyword | Resolves To |
|---------|-------------|
| `skills` | `.agent/skills/*/SKILL.md` |
| `workflows` | `.agent/workflows/*.md` |
| `docs` | `README.md`, `CHANGELOG.md`, `ROADMAP.md`, `docs/**/*.md` |
| `all` | All of the above |
| *(freeform)* | Keyword match against file names/content — confirm with user |

- **No arguments** → Ask: *"What should I optimize? (file path, directory, or keyword: `skills` | `workflows` | `docs` | `all`)"*
- **Exclusions**: Never optimize `AGENTS.md`, `DiSCOS.md`, or `CLARIFY-*/DEVELOP-*` files with status `draft`, `draft-self-reviewed`, or `clarifying`.
- **Skill loading**: Track loaded skills per session. Never reload already-loaded skills.

---

## 2. Analyze Targets (Dry Run)

Analyze all targets **before applying changes**.

**Severity classification** (used in steps below and in §4):

| Severity | Examples | Approval |
|----------|----------|----------|
| **Safe** | Filler removal, whitespace normalization, obvious redundancy | Auto-apply |
| **Moderate** | Condensing examples, restructuring, merging sections | Preview diff → apply |
| **Significant** | Removing sections, changing structure, altering meaning | Explicit approval |

For each target file:

1. **Read** full content.
2. **Measure baseline** — character count, estimated tokens (chars ÷ 4), readability (§5).
3. **Classify** content type → select strategy (§3f).
4. **Identify candidates** using §3 rules + content-type strategy.
5. **Classify severity** — assign each candidate a severity level using the table above.
6. **Cross-file scan** (multi-file only) — detect duplicate/near-duplicate content across targets (§3g).
7. **Dependency check** — verify all cross-references (`/review`, `/update`, skill names, file paths) resolve after proposed changes.

Present dry-run summary before proceeding to §4:

| File | Baseline Tokens | Candidates | Severity | Risk |
|------|-----------------|------------|----------|------|
| *target file* | X | N changes | Safe/Moderate/Significant | note |

---

## 3. Optimization Rules

Apply in order.

### 3a. Eliminate _(§Documentation: Anti-patterns)_

- Filler phrases: "in order to" → "to", "it is important to note that" → _(delete)_
- Redundant statements → keep clearest version
- Obvious comments restating structure → remove
- Empty/placeholder sections → remove
- Excessive hedging → definitive language or delete

### 3b. Condense _(§Documentation: Conciseness + §Context Management: Compression)_

- Verbose explanations → concise equivalents preserving key info
- Long examples → minimal representative; one per pattern
- Repeated patterns → one instance + "Same pattern for X, Y, Z"
- Paragraph lists → bullets (3+ items)
- Descriptive comparisons → tables
- Multi-sentence definitions → single-sentence

### 3c. Restructure _(§Documentation: Structure + Clarity)_

- **Inverted pyramid**: Key info first, detail after
- **Front-load**: Lead with actionable/important word
- **Visual hierarchy**: Headers, bold, code blocks
- **Parallel structure**: Consistent grammar across list items

### 3d. Enhance _(§DiSCOS: Excellence by Simplicity — Full Solution, Not Minimum Effort)_

Add content when absence reduces effectiveness or causes agent errors:

- Missing context that would prevent agent errors → add concisely
- Ambiguous instructions → clarify with precise wording
- Non-obvious patterns without examples → add minimal representative example _(skip for workflow files unless the example prevents consistent misexecution)_
- Incomplete cross-references → add missing links
- Edge cases that repeatedly cause failures → document explicitly

> **Gate**: Each addition must pass the test — *"Does removing this force a compensating action (extra tool call, follow-up question, or retry)?"* If yes, it stays. Additions follow tiered approval same as reductions.

### 3e. Preserve _(Never Optimize Away)_

- YAML frontmatter (all fields/values)
- Structural anchors (headings, IDs, cross-references used by workflows)
- Security-critical details
- Decision rationale (Conclusions > Reasoning)
- Acceptance criteria
- Code blocks, Mermaid diagrams, data tables
- Version identifiers (numbers, dates, changelog entries)
- Cross-references (workflow names, skill references, file paths)

### 3f. Content-Type Strategies

Apply after general rules (§3a–§3e):

| Type | Priority | Strategy |
|------|----------|----------|
| **Skill** (reference) | Lookup speed | Tables over prose. Alphabetical within sections. Inline examples only if essential. |
| **Workflow** (procedural) | Execution clarity | Strict numbered steps. Clear conditionals — form: `If <X>, do Y; else do Z`. Never nest beyond two levels. Eliminate narrative between steps. |
| **Doc** (narrative) | Scannability | Inverted pyramid per section. Progressive disclosure. TOC for 5+ sections. |
| **Report** (analytical) | Data density | Tables for data. Executive summary first. Minimize prose around findings. |
| **Todo** (ephemeral) | Brevity | Strip context older than current sprint. Compress completed items to one-line summaries. |

### 3g. Cross-File Deduplication _(multi-file scopes only)_

1. **Detect**: Flag blocks duplicated or near-duplicated (>70% similarity) across files.
2. **Report**: List duplicate pairs with file paths and similarity %.
3. **Recommend**: Suggest shared reference file or consolidation with cross-link. Never auto-apply.

---

## 4. Apply Changes

> **Prerequisite**: §2 dry-run summary must be presented before any changes are applied.

| Severity | Action |
|----------|--------|
| **Safe** | Apply directly |
| **Moderate** | Show diff → apply after approval |
| **Significant** | Show diff + rationale → wait for explicit approval |
| **Mixed** | Group by severity; apply safe immediately, present rest together |

Post-apply:
1. Verify all cross-references from §2.7 resolve. Report broken references.
2. **Idempotency check** — re-run §2 analysis on changed files. If new candidates surface, flag as non-idempotent and report before stopping.

> **Rollback**: Changes are uncommitted. `git diff` to inspect, `git checkout -- <file>` to revert.

---

## 5. Report Metrics

| File | Before (tokens) | After (tokens) | Δ Change | Quality Score | Severity |
|------|-----------------|----------------|----------|---------------|----------|
| *target file* | X | Y | −Z% or +Z% | score/100 | level |
| **Total** | **X** | **Y** | **net Δ** | **avg** | — |

> Δ Change may be **positive** (token increase) when Enhancement Rules (§3d) improve effectiveness. This is a valid optimization outcome.

**Quality Score** (0–100) — weighted composite:
- Scannable structure ratio: % in tables/lists/code blocks (30%)
- Avg words per bullet: target ≤15 (25%)
- Heading density: per 500 tokens, target 1–3 (25%)
- Verb-first imperatives: % of step/instruction lines starting with an imperative verb (20%)

**Actionability Gate** (binary pass/fail, evaluated before Quality Score):
- [ ] Every step has a clear next action
- [ ] No instruction is ambiguous (single valid interpretation)
- [ ] No known edge cases are undocumented

> Fail on any unchecked item → iterate before reporting score.

Already-optimal files: note as `— (already optimal)` and skip.

---

## 6. Quality Gate

Apply Priority Stack to every optimized file — evaluate top-down, higher gate failure blocks lower:

| Gate | Question | Failure Action |
|------|----------|----------------|
| **Security** | Are security-critical details preserved or improved? | Revert immediately |
| **Correctness** | Will the optimized content produce correct agent behavior? | Revert or restore missing context |
| **Clarity** | Is the content more understandable after optimization? | Iterate — don't apply if no clarity gain |
| **Simplicity** | Was accidental complexity removed without losing essential complexity? | Iterate |
| **Performance** | Was context efficiency improved (better outcomes per token)? | Accept as-is if upper gates pass |

For **moderate or significant** changes that alter workflow steps or skill lookup tables: additionally run full `/review` on modified files.

| Outcome | Action |
|---------|--------|
| ✅ or ⚠️ | Complete — report to user |
| 🔴 Critical | Revert, re-optimize with less aggression, re-review |

---

## 7. Operational Guarantees

| Property | Guarantee |
|----------|-----------|
| **Idempotent** | Re-running produces no further changes |
| **Reversible** | Git-tracked; no backup files created |
| **Non-destructive** | Frontmatter, structural anchors, security details, decision rationale, versions preserved |
| **Scope-aware** | Only touches files matching resolved scope |
| **Observable** | Metrics report shows before/after per file with Δ change and quality scores |
| **Safe by default** | Tiered approval prevents unintended semantic changes |
| **Reference-safe** | Cross-references verified before and after changes |
| **No duplicate loads** | Skills loaded once per session; tracked to prevent redundant reads |