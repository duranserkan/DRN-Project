---
description: Detect changed files from the last N commits and delegate to /update with file-based scope
---

> **Trigger**: `/update-last <N>` (N is commit count).
> **Purpose**: Derives `/update` scope from recent git history.
> See also: [Operating Model](./_shared/workflow-operating-model.md)
> **Estimated context: ~0.4K tokens**

---

## 1. Validate Input
Apply the shared Startup Gate before work: read `AGENTS.md`, `.agent/rules/DiSCOS.md` when present, `.agent/repository-profile.md` when present, this workflow, and the shared operating model.

Verify N is a positive integer. If missing/invalid, respond:
```text
⚠️ Usage: /update-last <N> — N must be a positive integer.
```

---

## 2. Collect Changed Files
Inspect changes across the last N commits:
```bash
git diff --name-status HEAD~<N>..HEAD
```
If the range is unavailable, fall back to `git log --name-status --pretty=format: -<N>`.

---

## 3. Build Scope
1. Filter, deduplicate, and sort the file list while preserving change status (`A`, `M`, `D`, `R`).
   *Note: Deleted and renamed-from files are included so `/update` can detect removed skills/projects/workflows.*
2. If no files changed, stop:
   ```text
   ℹ️ No files changed in the last <N> commit(s). Nothing to sync.
   ```
3. Format scope:
   ```text
   files: <comma-separated paths>
   ```

---

## 4. Report & Delegate
Report changes and delegate:
```markdown
## 🔍 update-last: Detected Changes
**Commits inspected**: <N> | **Changed files** (<count>)
| Status | File |
|---|---|
| M | <file1> |
**Scope**: `files: <comma-separated>`
Delegating to `/update files: <scope>` …
```
Delegate to `/update` by loading the workflow:
```bash
Read file: .agent/workflows/update.md
```
Pass the `files: <comma-separated>` scope argument.

---

## Operational Notes
- **Read-only**: Reads history; does not modify git state.
- **Composable / Fail-fast**: Stops on empty changesets or invalid inputs.
