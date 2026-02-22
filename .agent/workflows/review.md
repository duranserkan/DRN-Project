---
description: Review git staged changes, diff the current feature/fix branch against develop, or a specified task using Priority Stack and project review skills. Use DiSCOS, AGENTS.md and repository skills guidance
---

> **Estimated context: ~1.1K tokens** (this workflow) + ~5.1K when skills load
>
> [!IMPORTANT]
> **Executive Presence governs every stage**: structured evaluation, evidence-based findings, honest verdicts, decisive recommendations.

## 1. Determine Scope

- **File path(s) provided** → Read the specified files directly; skip git analysis.
- **No arguments** → Determine dynamically:
  1. Not on `develop`/`master` → run `git diff develop...HEAD`. Works for all branch naming conventions.
  2. On `develop`/`master`, or branch diff is empty → fall back to `git diff --cached` (staged changes).
  3. Nothing staged and no branch diff → inform user and stop.
- **Task/description provided** → Identify relevant files via keywords, recent git history, or user guidance. Read minimal context only.
- **Re-review** (fixes from a prior review) → Scope evaluation to changed lines only. Detect re-review when: user mentions a prior review, says "review again" / "re-review" / "check my fixes", or the same file/scope was reviewed in the current session.

---

## 2. Load Review Skills

```text
view_file .agent/skills/basic-code-review/SKILL.md
view_file .agent/skills/basic-security-checklist/SKILL.md
view_file .agent/skills/basic-documentation/SKILL.md
view_file .agent/skills/basic-git-conventions/SKILL.md
```

Single source of truth — do not duplicate criteria here.

---

## 3. Analyze

- **Branch/Staged**: Run `--stat` first for overview, then full diff. Read surrounding file context as needed. Note: deleted files appear as pure removals — confirm no dangling references remain.
- **Specified task**: Read identified files; understand change scope.
- If diff exceeds ~500 lines, split into logical file groups, evaluate each, then synthesize.

---

## 4. Evaluate

Apply loaded skills' criteria top-down:

1. **Priority Stack Gate** — Security → Correctness → Clarity → Simplicity → Performance. Higher failure blocks lower gates.
2. **Skill Checklists** — Run every relevant checklist; skip clearly irrelevant ones.

> **Re-review rule**: On re-review, apply steps 3–7 to **changed lines/constructs only**. Do not re-evaluate unchanged code through speculative lenses — this prevents fix → new-finding cascades.

> **Recommendation self-check**: Before reporting a 🟡 Suggestion, evaluate whether the proposed fix introduces more complexity than the finding warrants. If the cure is worse than the disease — tag it `[COMPLEXITY WARNING]`, recommend accepting the status quo, and demote to 🔵 Note. This rule does **not** apply to 🔴 Critical findings.

> **Alternative-comparison evaluation**: For every 🔴 Critical or 🟡 Suggestion, determine whether a simpler or more idiomatic alternative exists:
> 1. What is the current approach?
> 2. What is the best known alternative? (Established pattern, language idiom, framework feature, library convention)
> 3. TRIZ test — does the alternative satisfy both constraints without tradeoff? If yes → recommend it.
> 4. If no better alternative exists → state "no better alternative identified" and proceed.
>
> When a finding has a demonstrably better alternative, tag it `[IMPROVABLE]` alongside its severity. Include concrete refactoring direction in the fix field. Gate: the improvement must pass the recommendation self-check — if the refactoring introduces more complexity than it resolves, demote or drop.

3. **Pre-Mortem** — *"If this causes an incident in 6 months, what was the root cause?"*
4. **Second-Order Thinking** — *"What are the consequences of this change's consequences?"*
5. **Five Whys** — Bug fixes only: *"Does this fix the root cause or the symptom?"*
6. **Systems Thinking** — Integration points, shared state, cross-layer effects.
7. **What-If Analysis** — Null / huge / malicious / concurrent inputs.

---

## 5. Produce Review Report

Write as an artifact in task mode. Omit empty sections.

| Verdict | Condition |
|---|---|
| ✅ Approve | No 🔴 Critical findings |
| ⚠️ Approve with Comments | No 🔴 Critical, but 🟡 Suggestions present |
| ❌ Request Changes | Any 🔴 Critical finding |
| ✅ Converged | Re-review: no 🔴 Critical (remaining 🟡 accepted as-is) |

> **Iteration limit**: Max 2 review cycles (initial + 1 re-review). After re-review, remaining 🟡 suggestions are accepted — do not iterate further unless user explicitly requests another pass or 🔴 Critical findings remain.

```markdown
## Review Summary
**Scope**: [branch changes | staged changes | task description]
**Verdict**: ✅ / ⚠️ / ❌

## Findings
### 🔴 Critical
- [file · location · issue · fix · `[IMPROVABLE]` if better alternative exists]

### 🟡 Suggestions
- [file · location · issue · fix · `[IMPROVABLE]` if better alternative exists]

### 🟢 Positive
- [pattern observed]

### 🔵 Notes
- [informational observation · no action required]

## Pre-Mortem Risks
- [risk or "None"]

## Recommendations
- [next steps]
```
