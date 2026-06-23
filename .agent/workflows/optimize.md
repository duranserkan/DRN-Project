---
description: Optimize agent-consumed content for correctness per token while preserving source-owned rules and workflow gates
---

> **Trigger**: `/optimize [scope]`
> **Mission**: Maximize correct output per token.
> **DiSCOS**: Preserve conclusions, decisions, and patterns.
> **TRIZ**: Optimize for actionability, not shortest text.
> See also: [Operating Model](./_shared/workflow-operating-model.md), [`/review`](./review.md)
> **Estimated context: ~1.1K tokens**
>
> [!IMPORTANT]
> Be structured, evidenced, honest, and decisive.

## 1. Scope

Run Startup Gate once. Load scoped files only.

| Scope | Target |
|---|---|
| File path | Exact file path |
| `skills` | `.agent/skills/*/SKILL.md` |
| `workflows` | `.agent/workflows/**/*.md`, including `_shared/` |
| `docs` | `README.md`, `CHANGELOG.md`, `ROADMAP.md`, `docs/**/*.md` |
| `all` | Skills, workflows, and docs |
| None | Ask for scope and stop |
| Caller | From `/review`, CAD, or `/goal`: preserve caller gates and treat findings as evidence |

Invariant: Do not optimize `AGENTS.md`, `DiSCOS.md`, or `.agent/temp/` files unless explicitly scoped. Require approval and preserve all metadata: status, hashes, source keys.

## 2. Preview

Preview before edits.

| Severity | Examples | Gate |
|---|---|---|
| Safe | Whitespace, filler, duplicate phrasing | Confirm preview |
| Moderate | Structural condensation, text merging | Show diff, confirm |
| Significant | Content removal, semantic change | Show diff and rationale; require approval |
| Mixed | Varied severities | Apply confirmed changes only |

For each target:
1. Estimate baseline tokens: `chars / 4`.
2. Map candidates to Section 3 rules.
3. Classify severity and risk.
4. Reject net complexity; mark additions `[COMPLEXITY WARNING]`.
5. Apply TRIZ and choose the simplest source-owned option.
6. Report multi-file similarity >70%; consolidate only on confirmation.
7. Validate references, metadata, and workflow gates.
8. Label each action `optimize`, `defer`, or `reject` with one-sentence rationale.

Preview summary:

| File | Baseline Tokens | Proposed Changes | Severity | Risk |
|---|---|---|---|---|

## 3. Rules

Apply in order:
- Eliminate filler, hedging, placeholders, duplicate phrasing, and comment restatements.
- Condense tables, bullets, definitions, and examples.
- Restructure by front-loading decisions, using parallel grammar, and limiting nesting to 2 levels.
- Enhance only to fix errors, ambiguity, broken links, or edge cases.
- Simplify indirection, deep nesting, redundant checks, and compensating complexity.
- Deduplicate similarities >70%; report source and confirm consolidation.

Keep YAML frontmatter, anchors, links, security instructions, design rationale, versions, code blocks, diagrams, metadata, and source keys (`source_status`, `source_updated`, `source_sha256`).

| Type | Standard |
|---|---|
| Skill | Tables, alphabetical keys, minimal examples |
| Workflow | Direct steps and conditional directives: `If X, do Y` |
| Doc | Inverted pyramid, progressive disclosure, TOC when sections >=5 |
| Report | Executive summary plus tables |
| Todo | Active context only; one-line completion logs |

## 4. Apply

Edit only after preview confirmation or explicit apply mode.

1. Record approved scope, candidates, and severity.
2. Verify references, links, and metadata.
3. Re-run preview checks for idempotency.
4. Run `git diff --check`.

Review integration: run `/review` on diffs for Moderate or Significant changes. Block completion on Critical findings; restart preview when blocked. Integrate minor feedback into metrics.

## 5. Metrics

| File | Pre-Tokens | Post-Tokens | Delta % | Quality Score | Severity |
|---|---|---|---|---|---|

Quality Score, 0-100: Layout/Structure 30%, Conciseness 25% with average bullet length <=15 words, Heading density 25%, Imperative phrasing 20%.

Actionability Gate: score is 0 if any step lacks a clear action, contains ambiguity, or omits documented edge cases.

## 6. Guarantees

Ensure edits remain:
- Idempotent and preview-driven.
- Non-destructive and Git-tracked.
- Strictly scoped.
- Respectful of invariants.

Apply Priority Stack and `/review` to Moderate and Significant changes.
