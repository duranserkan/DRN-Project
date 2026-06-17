---
description: Optimize agent-consumed content for correctness per token while preserving source-owned rules and workflow gates
---

> **Trigger**: `/optimize [scope]`
> **Mission**: maximize correct output per token.
> **Principle**: DiSCOS Context Management: preserve conclusions, decisions, and patterns before instances.
> **TRIZ Contradiction**: Brevity vs. completeness -> optimize for actionability, not shortest text.
> See also: [Operating Model](./_shared/workflow-operating-model.md), [`/review`](./review.md)
> [!IMPORTANT]
> **Executive Presence governs every stage**: structured analysis, evidence-based optimization, honest metrics, decisive reporting.
> **Estimated context: ~1.2K tokens**

---

## 1. Resolve Scope
Run the shared Startup Gate once, reuse loaded context, and load only skills needed for the scoped material.

| Invocation | Scope Rule |
|---|---|
| File or directory path | Target exactly that scope. |
| `skills` | `.agent/skills/*/SKILL.md` |
| `workflows` | `.agent/workflows/**/*.md`, including `_shared/` |
| `docs` | `README.md`, `CHANGELOG.md`, `ROADMAP.md`, `docs/**/*.md` |
| `all` | Skills, workflows, and docs above. |
| No arguments | Ask for scope and stop. |
| From `/review`, CAD, or `/goal` | Treat findings as candidate evidence; preserve caller gates and report routed proof back. |

Never optimize `AGENTS.md`, `DiSCOS.md`, or `.agent/temp/CLARIFY-*` / `.agent/temp/DEVELOP-*` by default. If explicitly scoped, require approval and preserve lifecycle metadata (`status`, `stale`, `needs_review`, `source_*`, hashes).

---

## 2. Analyze Targets (Preview)
Analyze targets before applying changes. No files are edited in this section.

| Severity | Examples | Approval |
|----------|----------|----------|
| **Safe** | Filler, whitespace, obvious redundancy | Preview; apply only after confirmation |
| **Moderate** | Condensing examples, restructuring, merging | Preview diff -> apply only after confirmation |
| **Significant** | Removing sections, changing meaning/structure | Explicit approval |

For each target:
1. Measure baseline tokens (`chars / 4`).
2. Select a content strategy and candidates from §3.
3. Classify severity and risk.
4. Drop candidates that add net complexity; tag `[COMPLEXITY WARNING]` if reporting them.
5. Compare alternatives with TRIZ; keep the simpler source-owned option.
6. Scan duplicates across multi-file scopes; recommend consolidation only with confirmation.
7. Verify cross-references and preserved metadata.
8. Map supplied review findings to `optimize`, `defer`, or `reject` with one-line reasons.

Present preview summary before proceeding:
| File | Baseline Tokens | Candidates | Severity | Risk |
|---|---|---|---|---|

---

## 3. Optimization Rules
Apply in order:

| Rule | Action |
|---|---|
| Eliminate | Remove filler, hedging, placeholders, comments that restate structure, and duplicate statements. |
| Condense | Prefer tables/bullets, one-sentence definitions, one representative example, and "same pattern" references. |
| Restructure | Front-load decisions, use parallel grammar, max 2 nesting levels for workflows. |
| Enhance | Add only when omission causes agent errors, ambiguity, broken references, or missing edge cases. |
| Simplify | Remove valueless indirection, deep nesting, redundant guards, and compensating complexity. |
| Deduplicate | For multi-file scopes, report similarity >70% and recommend a source file; never consolidate without confirmation. |

Preserve YAML frontmatter, anchors, cross-references, security details, decision rationale, versions, acceptance criteria, code blocks, diagrams, tables, lifecycle metadata, active handoff content, and `.agent/temp/DEVELOP-*` source-tracking keys (`source_status`, `source_updated`, `source_sha256`).

| Type | Strategy |
|---|---|
| Skill | Tables over prose; alphabetical order; examples only when essential. |
| Workflow | Numbered steps and `If X, do Y`; no narrative filler. |
| Doc | Inverted pyramid; progressive disclosure; TOC for 5+ sections. |
| Report | Executive summary first; tables for data. |
| Todo | Current-sprint context only; one-line completed summaries. |

---

## 4. Apply Changes
Apply only when the user confirms the preview or invokes an explicit apply mode. Safe edits still require confirmation.

| Severity | Action |
|----------|--------|
| **Safe** | Apply after preview confirmation |
| **Moderate** | Show diff -> apply after approval |
| **Significant** | Show diff + rationale -> wait for explicit approval |
| **Mixed** | Apply only confirmed items; leave the rest untouched |

Approval must record previewed scope, candidate set, and severity. Post-apply, verify cross-references, re-run §2 for idempotency, and run `git diff --check` unless blocked.

### Review Loop
For Moderate or Significant workflow/skill changes, run `/review` on the optimized diff. 🔴 Critical findings block completion and feed the next preview/apply cycle. ✅ or ⚠️ satisfies the `/optimize` quality gate; carry remaining 🟡 findings into metrics.

---

## 5. Report Metrics
| File | Before (tokens) | After (tokens) | Δ Change | Quality Score | Severity |
|---|---|---|---|---|---|

**Quality Score** (0–100): Scannable structure % (30%) + Avg words/bullet ≤15 (25%) + Heading density (25%) + Verb-first imperatives (20%).
**Actionability Gate**: Every step has a clear next action, no ambiguity, and documented edge cases. Must pass before scoring.

---

## 6. Operational Guarantees
- **Idempotent**: Re-running on already-optimized content produces no further changes.
- **Reversible**: Changes are git-tracked; no backup files are created.
- **Non-destructive**: Preserves frontmatter, anchors, security, versions.
- **Scope-aware**: Touches only scoped files.
- **Observable**: Metrics report shows before/after and scores.
- **Safe by default**: Preview-first; no edits before confirmation.
- **Quality-gated**: Priority Stack and `/review` validate Moderate/Significant workflow or skill edits.
