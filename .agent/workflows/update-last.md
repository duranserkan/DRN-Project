---
description: Detect changed files from the last N commits and delegate to /update with file-based scope
---

> **Trigger**: `/update-last <N>` (N is commit count).
> **Purpose**: Derives `/update` scope from recent git history.
> **Estimated context: ~0.4K tokens**

---

## 1. Validate Input
Verify N is a positive integer. If missing/invalid, respond:
```text
⚠️ Usage: /update-last <N> — N must be a positive integer.
```

---

## 2. Collect Changed Files
Inspect changes across the last N commits:
```bash
git log --name-only --pretty=format: -<N>
```

---

## 3. Build Scope
1. Filter, deduplicate, and sort the file list.
   *Note: Deleted files are included; they fail discovery matches and are skipped.*
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
- <file1>
**Scope**: `files: <comma-separated>`
Delegating to `/update files: <scope>` …
```
Execute `/update` by loading the workflow:
```bash
view_file .agent/workflows/update.md
```
Pass the `files: <comma-separated>` scope argument.

---

## Operational Notes
- **Read-only**: Reads history; does not modify git state.
- **Composable / Fail-fast**: Stops on empty changesets or invalid inputs.
