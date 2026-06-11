---
description: Review git staged changes, diff the current feature/fix branch against develop, or a specified task using Priority Stack and project review skills. Use DiSCOS, AGENTS.md and repository skills guidance
---

> **Estimated context: ~0.8K tokens** (this workflow) + ~5.1K when skills load
>
> [!IMPORTANT]
> **Executive Presence governs every stage**: structured evaluation, evidence-based findings, honest verdicts, decisive recommendations.

---

## 1. Determine Scope
- **File path(s) provided**: Read specified files directly; skip git analysis.
- **No arguments**:
  1. Not on `develop`/`master` → Run `git diff develop...HEAD`.
  2. On `develop`/`master` (or empty diff) → Run `git diff --cached` (staged changes).
  3. Empty → Inform user and stop.
- **Task/description provided**: Identify files via keywords/git history; read minimal context.
- **Re-review**: Evaluate changed lines only. Triggered when user asks to "re-review", "check fixes", or scope was recently reviewed.

---

## 2. Load Review Skills
```text
view_file .agent/skills/basic-code-review/SKILL.md
view_file .agent/skills/basic-security-checklist/SKILL.md
view_file .agent/skills/basic-documentation/SKILL.md
view_file .agent/skills/basic-git-conventions/SKILL.md
```
*Single source of truth — do not duplicate criteria.*

---

## 3. Analyze
- **Branch/Staged**: Run `git diff --stat` first, then full diff. Confirm no dangling references exist for deleted files.
- **Specified task**: Read files to understand changes.
- *Large diffs*: If > 500 lines, split into logical file groups, evaluate individually, then synthesize.

---

## 4. Evaluate
Apply loaded skills' criteria top-down:
1. **Priority Stack Gate**: Security → Correctness → Clarity → Simplicity → Performance. Failure blocks lower gates.
2. **Skill Checklists**: Run all relevant checklists.

> **Re-review rule**: Evaluate **changed lines/constructs only**. Do not evaluate unchanged code — prevents fix → new-finding cascades.

> **Recommendation self-check**: Do not recommend a 🟡 Suggestion if the fix is more complex than the finding. If so, tag it `[COMPLEXITY WARNING]`, recommend accepting status quo, and demote to 🔵 Note. (Does not apply to 🔴 Critical).

> **Alternative-comparison**: For 🔴 Critical or 🟡 Suggestions, check if a simpler alternative exists:
> 1. Current approach vs best known alternative (pattern, idiom, framework feature).
> 2. TRIZ test: satisfies constraints without tradeoff? If yes, recommend it.
> 3. If no better alternative: state "no better alternative identified" and proceed.
> Tag findings with demonstrably better alternatives as `[IMPROVABLE]` and include refactoring direction. Must pass self-check.

3. **Pre-Mortem**: Root cause of potential failure in 6 months?
4. **Second-Order Thinking**: Consequences of consequences?
5. **Five Whys** (bug fixes): Target root cause or symptom?
6. **Systems Thinking**: Shared state, cross-layer effects, integration.
7. **What-If Analysis**: Null, huge, malicious, concurrent inputs.

---

## 5. Produce Review Report
Write as an artifact in task mode. Omit empty sections.

| Verdict | Condition |
|---|---|
| ✅ Approve | No 🔴 Critical findings |
| ⚠️ Approve with Comments | No 🔴 Critical, but 🟡 Suggestions present |
| ❌ Request Changes | Any 🔴 Critical finding |
| ✅ Converged | Re-review: no new 🔴 Critical (remaining 🟡 accepted) |

> **Iteration limit**: Max 2 cycles (initial + 1 re-review). Remaining 🟡 are accepted after re-review.

### `/update` State Hook
When `/review` is invoked by `/update`, it owns the review state transition:
- Reviewing `.agent/update-plan.md` from `Status: ready`: if no 🔴 Critical findings, update the plan header to `Status: plan-reviewed`; otherwise leave it `ready`.
- Reviewing `/update` changes from `Status: done`: if no 🔴 Critical findings, update the plan header to `Status: reviewed`; otherwise leave it `done`.

### Report Template
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
