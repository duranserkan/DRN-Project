---
description: Review git staged changes, diff the current feature/fix branch against develop, or a specified task using Priority Stack and project review skills
---

## 1. Determine Scope

- **No extra arguments** → Determine scope dynamically based on git state:
  1. Check if the current branch is **not** `develop` or `master`. If so, attempt `git diff develop...HEAD` — this covers all branch naming conventions (`feature/*`, `fix/*`, `chore/*`, `docs/*`, and freeform branches).
  2. If on `develop`, `master`, or the branch diff returns nothing, fall back to reviewing **git staged changes** (`git diff --cached`).
  3. If nothing is staged and no branch diff exists, inform the user and stop.
- **Task/description provided** → Review the **described scope**.
  - Identify relevant files by keywords, recent git history, or user guidance.
  - Read only the minimal context needed.

---

## 2. Load Review Skills

Read to internalize review criteria (single source of truth — do not duplicate content here):

- `view_file .agent/skills/basic-code-review/SKILL.md`
- `view_file .agent/skills/basic-security-checklist/SKILL.md`
- `view_file .agent/skills/basic-documentation/SKILL.md`
- `view_file .agent/skills/basic-git-conventions/SKILL.md`

---

## 3. Analyze

- **Branch or Staged changes** — Depending on the scope determined in Step 1:
  - Run `git diff develop...HEAD --stat` or `git diff --cached --stat` for an overview.
  - Run `git diff develop...HEAD` or `git diff --cached` for the full diff. Read surrounding context of changed files as needed.
- **Specified task** — Read identified files and understand change scope.
- If diff exceeds ~500 lines, break into logical file groups and evaluate each group independently before synthesizing the final verdict.

---

## 4. Evaluate

Apply loaded skills' review criteria to the scope:

1. **Priority Stack Gate** — Evaluate top-down: Security → Correctness → Clarity → Simplicity → Performance. A higher-level failure blocks lower gates.
2. **Skill Checklists** — Run every relevant checklist from loaded skills. Skip checklists clearly irrelevant to the scope.
3. **Pre-Mortem** — *"If this change causes an incident in 6 months, what was the root cause?"*
4. **Second-Order Thinking** — *"What are the consequences of this change's consequences?"*
5. **Five Whys** — For bug fixes: *"Does this fix the root cause or just the symptom?"*
6. **Systems Thinking** — Evaluate systemic impact: integration points, shared state, cross-layer effects.
7. **What-If Analysis** — Probe edge cases: null / huge / malicious / concurrent inputs.

---

## 5. Produce Review Report

Write a structured review report (as an artifact when in task mode). Omit empty sections.

| Verdict | When |
|---|---|
| ✅ Approve | No 🔴 Critical findings |
| ⚠️ Approve with Comments | No 🔴 Critical, but 🟡 Suggestions present |
| ❌ Request Changes | Any 🔴 Critical finding |

```markdown
## Review Summary
**Scope**: [branch changes | staged changes | task description]
**Verdict**: ✅ / ⚠️ / ❌

## Findings
### 🔴 Critical
- [file · location · issue · fix]

### 🟡 Suggestions
- [file · location · issue · fix]

### 🟢 Positive
- [pattern observed]

## Pre-Mortem Risks
- [risk or "None"]

## Recommendations
- [next steps]
```
