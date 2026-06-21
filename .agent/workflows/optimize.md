---
description: Optimize agent-consumed content for correctness per token while preserving source-owned rules and workflow gates
---

> **Trigger**: `/optimize [scope]`
> **Mission**: Maximize correct output per token.
> **DiSCOS**: Preserve conclusions, decisions, and patterns.
> **TRIZ**: Optimize for actionability, not shortest text.
> See also: [Operating Model](./_shared/workflow-operating-model.md), [`/review`](./review.md)
> **Estimated context: ~0.5K tokens**
>
> [!IMPORTANT]
> Be structured, evidenced, honest, and decisive.

## 1. Scope
Execute Startup Gate once. Load scoped files only.

| Scope | Target |
|---|---|
| File Path | Exact file path |
| `skills` | `.agent/skills/*/SKILL.md` |
| `workflows` | `.agent/workflows/**/*.md` (including `_shared/`) |
| `docs` | `README.md`, `CHANGELOG.md`, `ROADMAP.md`, `docs/**/*.md` |
| `all` | All skills, workflows, and docs |
| None | Prompt for scope and stop |
| Caller | From `/review`, CAD, or `/goal`: preserve caller gates, treat as evidence |

*Invariant*: Do not optimize `AGENTS.md`, `DiSCOS.md`, or `.agent/temp/` (CLARIFY/DEVELOP) files unless explicitly scoped. Require approval and preserve all metadata (status, hashes, source keys).

## 2. Preview
Perform preview prior to edits.

| Severity | Scope Examples | Gate |
|---|---|---|
| **Safe** | Whitespace, filler, duplicate phrasing | Confirm preview |
| **Moderate** | Structural condensation, text merging | Show diff, confirm |
| **Significant** | Content removal, semantic changes | Show diff + rationale, require approval |
| **Mixed** | Varied severities | Apply confirmed changes only |

For each target:
1. Estimate baseline tokens (`chars / 4`).
2. Map to Section 3 rules and candidates.
3. Classify severity and risk.
4. Reject net complexity; mark additions as `[COMPLEXITY WARNING]`.
5. Apply TRIZ; select the simplest source-owned option.
6. Report multi-file similarity >70%; consolidate only on confirmation.
7. Validate cross-references, metadata, and workflow gates.
8. Label actions as `optimize`, `defer`, or `reject` with single-sentence rationale.

Output preview summary:
| File | Baseline Tokens | Proposed Changes | Severity | Risk |
|---|---|---|---|---|

## 3. Optimization Rules
Apply rules in priority order:
- **Eliminate**: Filler, hedging, placeholders, duplicate phrasing, comment restatements.
- **Condense**: Tables, bullets, single-sentence definitions, single examples.
- **Restructure**: Front-load decisions, apply parallel grammar, restrict nesting to 2 levels.
- **Enhance**: Add content only to resolve errors, ambiguity, broken links, or edge cases.
- **Simplify**: Strip indirection, deep nesting, redundant checks, compensating complexity.
- **Deduplicate**: Identify similarities >70% across files; report source and confirm consolidation.

*Invariants*: Retain YAML frontmatter, anchors, links, security instructions, design rationale, versions, code blocks, diagrams, metadata, and source keys (`source_status`, `source_updated`, `source_sha256`).

| Type | Formatting Standard |
|---|---|
| **Skill** | Tabular format, alphabetical keys, minimal reference examples |
| **Workflow** | Direct steps and conditional directives (`If X, do Y`), no narrative filler |
| **Doc** | Inverted pyramid layout, progressive disclosure, TOC if sections >= 5 |
| **Report** | Executive summary header, tabular data representation |
| **Todo** | Active context only, single-line task completion logs |

## 4. Apply
Execute edits only after preview confirmation or explicit apply mode.
1. Record approved scope, candidate set, and severity.
2. Verify references, links, and file metadata.
3. Re-run Section 2 checks for idempotency.
4. Execute `git diff --check` to validate whitespace.

### Review Integration
Run `/review` on diffs for all Moderate or Significant changes. Block completion on critical findings; restart the preview cycle if blocked. Integrate minor feedback directly into metrics.

## 5. Metrics
| File | Pre-Tokens | Post-Tokens | Δ % | Quality Score | Severity |
|---|---|---|---|---|---|

- **Quality Score calculation (0-100)**: Layout/Structure (30%) + Conciseness (avg words/bullet <= 15) (25%) + Heading density (25%) + Imperative phrasing (20%).
- **Actionability Gate**: Zero score if any step lacks clear actions, contains ambiguity, or omits documented edge cases.

## 6. Guarantees
Ensure all edits remain:
- Idempotent and preview-driven.
- Non-destructive and Git-tracked.
- Scoped strictly to target files.
- Respectful of invariants (frontmatter, anchors, security metadata, versions).

Apply Priority Stack and `/review` workflows to all Moderate and Significant changes.
