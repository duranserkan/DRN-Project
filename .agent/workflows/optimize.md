---
description: Optimize agent-consumed content using DiSCOS and AGENTS.md (skills, workflows, docs, reports, todos) — reduce waste, enhance effectiveness, maximize context efficiency
---

> **Trigger**: `/optimize <scope>` or `/optimize` (prompts for scope).
> **Mission**: Maximize context efficiency (Reduce waste, Enhance effectiveness).
> **Principle**: DiSCOS Context Management (Preserve: Conclusions > Decisions > Patterns > Instances).
> **TRIZ Contradiction**: Brevity vs. Completeness → optimize for correct output per token.
> See also: [Operating Model](./_shared/workflow-operating-model.md)
> [!IMPORTANT]
> **Executive Presence governs every stage**: structured analysis, evidence-based optimization, honest metrics, decisive reporting.
> **Estimated context: ~1.5K tokens**

---

## 1. Resolve Scope
- **File/directory path**: Target specified files/directory.
- **Content-type keyword**:
  | Keyword | Resolves To |
  |---------|-------------|
  | `skills` | `.agent/skills/*/SKILL.md` |
  | `workflows` | `.agent/workflows/**/*.md` (including `_shared/`) |
  | `docs` | `README.md`, `CHANGELOG.md`, `ROADMAP.md`, `docs/**/*.md` |
  | `all` | All of the above |
- **No arguments**: Ask user for target.
- **Exclusions**: Never optimize `AGENTS.md`, `DiSCOS.md`, or `.agent/temp/CLARIFY-*` / `.agent/temp/DEVELOP-*` files with status `draft`, `draft-self-reviewed`, or `clarifying`.
- **Skill loading**: Track loaded skills to prevent redundant reads.

---

## 2. Analyze Targets (Preview)
Analyze targets before applying changes. No files are edited in this section.

| Severity | Examples | Approval |
|----------|----------|----------|
| **Safe** | Filler, whitespace, obvious redundancy | Preview; apply only after confirmation |
| **Moderate** | Condensing examples, restructuring, merging | Preview diff -> apply only after confirmation |
| **Significant** | Removing sections, changing meaning/structure | Explicit approval |

For each target:
1. Read content and measure baseline (token count = chars ÷ 4).
2. Select strategy (§3f) and identify candidates per §3 rules.
3. Classify severity.
4. **Net-impact check**: If optimization adds complexity, tag `[COMPLEXITY WARNING]` and drop.
5. **Alternative-comparison**: Check if a structurally different approach is better. Apply TRIZ test.
6. **Cross-file scan**: Detect duplicates across files (§3g).
7. Verify cross-references.
Present preview summary before proceeding:
| File | Baseline Tokens | Candidates | Severity | Risk |
|---|---|---|---|---|

---

## 3. Optimization Rules
Apply in order:

### 3a. Eliminate
- Filler phrases ("in order to" → "to") and redundant statements.
- Comments restating structure, placeholders, and hedging.

### 3b. Condense
- prose to tables/bullets; keep definitions to a single sentence.
- limit examples to one minimal representative case per pattern.
- repeated patterns to one instance + "Same pattern for X".

### 3c. Restructure
- Use inverted pyramid, front-load keywords, visual hierarchy, and parallel grammar.

### 3d. Enhance
Add content only if its absence causes agent errors or degrades outcomes (clarify ambiguity, missing links, edge cases). Removal must not force a compensating action.

### 3e. Preserve (Never Optimize Away)
- YAML frontmatter, structural anchors, and cross-references.
- Security-critical details, decision rationale, and versions.
- Acceptance criteria, code blocks, diagrams, and tables.

### 3f. Content-Type Strategies
| Type | Strategy |
|------|----------|
| **Skill** | Tables over prose. Alphabetical order. Inline examples only if essential. |
| **Workflow** | Strict numbered steps. Clear `If X, do Y` conditionals. Max 2 nesting levels. No narrative. |
| **Doc** | Inverted pyramid. Progressive disclosure. TOC for 5+ sections. |
| **Report** | Tables for data. Executive summary first. Minimal prose. |
| **Todo** | Strip context older than current sprint. One-line completed summaries. |

### 3g. Cross-File Deduplication (Multi-file only)
1. **Detect**: Similarity > 70%.
2. **Report**: List duplicate pairs.
3. **Recommend**: Suggest reference file or consolidation. Never apply without confirmation.

### 3h. Accidental Complexity Removal
- Simplify indirection (inline valueless reference chains).
- Flatten deep nesting.
- Remove redundant guards.

---

## 4. Apply Changes
Apply only when the user confirms the preview or invokes an explicit apply mode. Safe edits still require confirmation.

| Severity | Action |
|----------|--------|
| **Safe** | Apply after preview confirmation |
| **Moderate** | Show diff -> apply after approval |
| **Significant** | Show diff + rationale -> wait for explicit approval |
| **Mixed** | Apply only confirmed items; leave the rest untouched |

Post-apply: verify cross-references, check idempotency (re-run §2), and run `git diff --check` unless blocked.

---

## 5. Report Metrics
| File | Before (tokens) | After (tokens) | Δ Change | Quality Score | Severity |
|---|---|---|---|---|---|

**Quality Score** (0–100): Scannable structure % (30%) + Avg words/bullet ≤15 (25%) + Heading density (25%) + Verb-first imperatives (20%).
**Actionability Gate**: Every step has a clear next action, no ambiguity, and documented edge cases. Must pass before scoring.

---

## 6. Quality Gate
Priority Stack: Security → Correctness → Clarity → Simplicity → Performance.
Run Alternatives as a separate self-check before finalizing recommendations.
Run `/review` on Moderate/Significant changes to workflows/skills. Must be ✅ or ⚠️.

---

## 7. Operational Guarantees
- **Idempotent**: Re-running on already-optimized content produces no further changes.
- **Reversible**: Changes are git-tracked; no backup files are created.
- **Non-destructive**: Preserves frontmatter, anchors, security, versions.
- **Scope-aware**: Touches only scoped files.
- **Observable**: Metrics report shows before/after and scores.
- **Safe by default**: Preview-first; no edits before confirmation.
- **Reference-safe / No duplicate loads**: Verified references, single skill load tracking.
