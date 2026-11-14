---
description: Detect changed files from the last N commits and delegate to /update with file-based scope
---

> **Trigger**: `/update-last <N>` where N is a commit count.
> **Purpose**: Derive `/update` scope from recent Git history.
> See also: [Operating Model](./_shared/workflow-operating-model.md)

## 1. Validate Input

Run the shared Startup Gate.

Require N to be a positive integer. If missing or invalid, respond:

```text
Usage: /update-last <N> -- N must be a positive integer.
```

---

## 2. Collect Changed Files

Select up to N commits from `HEAD` along first-parent history. Report the actual count if history is shorter. Stop on unavailable `HEAD` or failed history inspection.

Collect each selected commit's name-status records against its first parent. Compare a root commit against the empty tree. Include merge commits' changes against their first parent. Use NUL-delimited records and retain both paths of rename/copy records. Do not replace this union with an endpoint tree diff, which omits changes reverted within the range.

---

## 3. Build Scope

1. Keep each reported change status, including additions, modifications, deletions, renames, copies and type changes.
2. Parse records without shell splitting or treating quoted display paths as literal names. Reject malformed records and paths containing commas or control characters before producing the comma-separated scope. Deduplicate and sort paths.
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
Commits requested: <N> | First-parent commits inspected: <actual count> | Changed files: <count>
| Status | File |
|---|---|
| M | <file1> |
Scope: `files: <comma-separated>`
Delegating to `/update <scope>`.
```

Delegate by loading `.agent/workflows/update.md` and passing the `files: <comma-separated>` scope.

## Operational Notes

- Read-only: inspect history only. Do not modify Git state.
- Fail-fast: stop on invalid input or empty changesets.
