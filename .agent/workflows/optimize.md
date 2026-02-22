---
description: Optimize agent-consumed content using DiSCOS and AGENTS.md (skills, workflows, docs, reports, todos) — reduce waste, enhance effectiveness, maximize context efficiency
---

> **Trigger**: `/optimize <scope>` or `/optimize` (prompts for scope).
>
> **Mission**: Maximize context efficiency. Two vectors, equal weight: (1) **Reduce** waste, (2) **Enhance** effectiveness. Token count may rise when enhancement earns it.
>
> **Principle**: DiSCOS Context Management — Preserve: Conclusions > Decisions > Patterns > Instances.
>
> **TRIZ Contradiction**: Brevity vs. Completeness → optimize for **correct output per token**. If a cut forces compensating actions (extra tool calls, follow-ups, retries), it's counter-productive. Satisfy both constraints.
>
> [!IMPORTANT]
> **Executive Presence governs every stage**: structured analysis, evidence-based optimization, honest metrics, decisive reporting.
>
> **Estimated context: ~3.0K tokens** (this workflow)

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

**Severity classification** (governs §4 approval):

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
5. **Classify severity** per table above.
6. **Net-impact check** (Moderate/Significant) — if the optimization adds more complexity than it removes → tag `[COMPLEXITY WARNING]` and drop. Safe items exempt.
7. **Alternative-comparison** (Moderate/Significant) — is a structurally different approach more context-efficient?
   - Current structure vs. best known alternative (established pattern, simpler representation).
   - TRIZ test: alternative satisfies both brevity and completeness? → prefer it.
   - No better alternative? → proceed with original.
   - Alternative scores higher on §5 Quality Score? → replace candidate.
8. **Cross-file scan** (multi-file) — detect duplicates across targets (§3g).
9. **Dependency check** — verify all cross-references resolve post-change.

Present dry-run summary before proceeding to §4:

| File | Baseline Tokens | Candidates | Severity | Risk |
|------|-----------------|------------|----------|------|
| *target file* | X | N changes | Safe/Moderate/Significant | note |

---

## 3. Optimization Rules

Apply in order.

### 3a. Eliminate *(§Documentation: Anti-patterns)*

- Filler phrases: "in order to" → "to", "it is important to note that" → *(delete)*
- Redundant statements → keep clearest version
- Obvious comments restating structure → remove
- Empty/placeholder sections → remove
- Excessive hedging → definitive language or delete

### 3b. Condense *(§Documentation: Conciseness + §Context Management: Compression)*

- Verbose explanations → concise equivalents, key info intact
- Long examples → one minimal representative per pattern
- Repeated patterns → one instance + "Same pattern for X, Y, Z"
- Paragraph lists → bullets (3+ items)
- Descriptive comparisons → tables
- Multi-sentence definitions → single-sentence

### 3c. Restructure *(§Documentation: Structure + Clarity)*

- **Inverted pyramid**: Key info first, detail after
- **Front-load**: Lead with actionable/important word
- **Visual hierarchy**: Headers, bold, code blocks
- **Parallel structure**: Consistent grammar across list items

### 3d. Enhance *(§DiSCOS: Excellence by Simplicity — Full Solution, Not Minimum Effort)*

Add content when its absence causes agent errors or degrades outcomes:

- Missing context that would prevent agent errors → add concisely
- Ambiguous instructions → clarify with precise wording
- Non-obvious patterns → minimal example *(workflows: only if it prevents misexecution)*
- Incomplete cross-references → add links
- Edge cases that repeatedly cause failures → document explicitly

> **Gate**: *"Does removing this force a compensating action?"* If yes → keep. Additions follow same tiered approval as reductions.

### 3e. Preserve *(Never Optimize Away)*

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

### 3g. Cross-File Deduplication *(multi-file scopes only)*

1. **Detect**: Flag blocks duplicated or near-duplicated (>70% similarity) across files.
2. **Report**: List duplicate pairs with file paths and similarity %.
3. **Recommend**: Suggest shared reference file or consolidation with cross-link. Never auto-apply.

### 3h. Accidental Complexity Removal *(proactive, not just defensive)*

Actively seek and remove structure that adds cost without earning it:

1. **Unnecessary indirection** — valueless reference chains, wrappers, pass-throughs → inline or flatten.
2. **Over-engineered structure** — deep nesting where flat lists suffice → simplify to minimum nesting preserving clarity.
3. **Redundant guards** — duplicates of existing mechanisms → remove, cross-reference original.

> For verbose-to-concise transformations (prose → tables, multi-sentence → single-sentence), see §3b.

**Pattern**: Identify → compare simpler alternative → verify essentials preserved → apply per §4.

**Gate**: Simpler version must pass all §6 gates. If Simplicity gains but Correctness or Clarity degrades → keep original, document why.

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
1. Verify all cross-references resolve. Report any broken.
2. **Idempotency check** — re-run §2 on changed files. New candidates? → flag non-idempotent, report, stop.

> **Rollback**: Changes are uncommitted. `git diff` to inspect, `git checkout -- <file>` to revert.

---

## 5. Report Metrics

| File | Before (tokens) | After (tokens) | Δ Change | Quality Score | Severity |
|------|-----------------|----------------|----------|---------------|----------|
| *target file* | X | Y | −Z% or +Z% | score/100 | level |
| **Total** | **X** | **Y** | **net Δ** | **avg** | — |

> Positive Δ (token increase) is valid when §3d enhancements improve effectiveness.

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

Priority Stack per file — top-down, higher failure blocks lower:

| Gate | Question | Failure Action |
|------|----------|----------------|
| **Security** | Are security-critical details preserved or improved? | Revert immediately |
| **Correctness** | Will the optimized content produce correct agent behavior? | Revert or restore missing context |
| **Clarity** | Is the content more understandable after optimization? | Iterate — don't apply if no clarity gain |
| **Simplicity** | Was accidental complexity removed without losing essential complexity? | Iterate |
| **Alternatives** | Was §2.7 alternative-comparison analysis completed for all Moderate/Significant candidates? | Complete analysis before proceeding |
| **Performance** | Was context efficiency improved (better outcomes per token)? | Accept as-is if upper gates pass |

**Moderate/Significant** changes altering workflow steps or skill tables → run `/review` on modified files.

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
