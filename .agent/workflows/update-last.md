---
description: Detect changed files from the last N commits and delegate to /update with file-based scope
---

> **Trigger**: `/update-last <N>` where N is a commit count.
> **Purpose**: Derive `/update` scope from recent Git history.
> See also: [Operating Model](./_shared/workflow-operating-model.md)
> **Estimated context: ~0.4K tokens**

## 1. Validate Input

Run the shared Startup Gate: read `AGENTS.md`; read `.agent/rules/DiSCOS.md` and `.agent/repository-profile.md` when present; read this workflow and the operating model.

Require N to be a positive integer. If missing or invalid, respond:

```text
Usage: /update-last <N> -- N must be a positive integer.
```

---

## 2. Collect Changed Files

Inspect the last N commits:

```bash
git diff --name-status HEAD~<N>..HEAD
```

If the range is unavailable, fall back to:

```bash
git log --name-status --pretty=format: -<N>
```

---

## 3. Build Scope

1. Keep change status (`A`, `M`, `D`, `R`).
2. Filter, deduplicate, and sort paths.
3. Include deleted and renamed-from files so `/update` can detect removed skills, projects, or workflows.
4. Stop if no files changed:

   ```text
   No files changed in the last <N> commit(s). Nothing to sync.
   ```

5. Format scope:

   ```text
   files: <comma-separated paths>
   ```

---

## 4. Report And Delegate

```markdown
## update-last: Detected Changes
Commits inspected: <N> | Changed files: <count>
| Status | File |
|---|---|
| M | <file1> |
Scope: `files: <comma-separated>`
Delegating to `/update <scope>`.
```

Delegate by loading `.agent/workflows/update.md` and passing the `files: <comma-separated>` scope.

## Operational Notes

- Read-only: inspect history only; do not modify Git state.
- Fail-fast: stop on invalid input or empty changesets.
