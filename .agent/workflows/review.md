---
description: Review git staged changes, diff the current feature/fix branch against develop, or a specified task using Priority Stack and project review skills
---

## 1. Determine Scope

- **File path(s) provided** → Read the specified files directly; skip git analysis.
- **No arguments** → Determine dynamically:
  1. Not on `develop`/`master` → run `git diff develop...HEAD`. Works for all branch naming conventions.
  2. On `develop`/`master`, or branch diff is empty → fall back to `git diff --cached` (staged changes).
  3. Nothing staged and no branch diff → inform user and stop.
- **Task/description provided** → Identify relevant files via keywords, recent git history, or user guidance. Read minimal context only.

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

### 🔵 Notes
- [informational observation · no action required]

## Pre-Mortem Risks
- [risk or "None"]

## Recommendations
- [next steps]
```
