---
description: Review git staged changes or a specified task using Priority Stack and project review skills
---

## 1. Determine Scope

- **No extra arguments** → Review **git staged changes** (`git diff --cached`).
  - If nothing is staged, inform the user and stop.
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

- **Staged changes** — Run `git diff --cached --stat` for overview, then `git diff --cached` for full diff. Read surrounding context of changed files as needed.
- **Specified task** — Read identified files and understand change scope.
- If diff exceeds ~500 lines, break into logical file groups and evaluate each group independently before synthesizing the final verdict.

---

## 4. Evaluate

Apply loaded skills' review criteria to the scope:

1. **Priority Stack Gate** — Evaluate top-down (Security → Correctness → Clarity → Simplicity → Performance). A higher-level failure blocks lower gates. *(ref: basic-code-review)*
2. **Skill Checklists** — Run every relevant checklist from the loaded skills. **Skip** checklists clearly irrelevant to the scope (e.g., DI audit for docs-only changes). *(Currently includes: DDD Boundaries, DI Audit, DTT, Breaking Changes, Security Triggers, Git Conventions)*
3. **Pre-Mortem** — *"If this change causes an incident in 6 months, what was the root cause?"* Use the failure-mode table from basic-code-review. *(ref: basic-code-review)*
4. **Second-Order Thinking** — *"What are the consequences of this change's consequences?"* — catch ripple effects the diff doesn't show (e.g., a simplification that locks in an assumption downstream).
5. **Five Whys** — For bug fixes: *"Does this fix the root cause or just the symptom?"* — prevent recurring patches.
6. **Systems Thinking** — Evaluate systemic impact beyond the changed files — integration points, shared state, cross-layer effects. *(Optimize the whole, not the part.)*
7. **What-If Analysis** — Probe edge cases: *"What if the input is null / huge / malicious / concurrent?"* — complements the security and correctness gates.

---

## 5. Produce Review Report

Write a structured review report (as an artifact when in task mode).

### Verdict Rules

| Verdict | When |
|---|---|
| ✅ Approve | No 🔴 Critical findings |
| ⚠️ Approve with Comments | No 🔴 Critical, but notable 🟡 Suggestions to track |
| ❌ Request Changes | Any 🔴 Critical finding (Priority Stack gate failure) |

### Report Template

```markdown
## Review Summary

**Scope**: [staged changes | task description]
**Verdict**: ✅ Approve | ⚠️ Approve with Comments | ❌ Request Changes

## Findings

### 🔴 Critical (Blockers)
- [file, location, issue, recommended fix — or "None"]

### 🟡 Suggestions
- [file, location, issue, recommended fix — or "None"]

### 🟢 Positive
- [specific good patterns observed]

## Pre-Mortem Risks
- [identified risks or "None identified"]

## Recommendations
- [actionable next steps]
```

Omit empty sections. For 🔴/🟡 items include: file, location, issue, and recommended fix. For 🟢 items name the specific good pattern.