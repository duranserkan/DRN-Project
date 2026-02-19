---
description: Detect changed files from the last N commits and delegate to /update with file-based scope
---

> **Trigger**: `/update-last <N>` where `<N>` is the number of recent commits to inspect.
>
> **Purpose**: Automatically derives an `/update` scope from git history — no manual scope needed.

---

## 1. Validate Input

The user must provide a **positive integer** commit count. Examples:

- ✅ `/update-last 3`
- ❌ `/update-last` (missing count)
- ❌ `/update-last abc` (not a number)

If invalid, respond:

```
⚠️ Usage: /update-last <N> — where N is a positive integer (number of recent commits to inspect).
```

---

## 2. Collect Changed Files

// turbo
```
git log --name-only --pretty=format: -<N>
```

> Replace `<N>` with the user-provided commit count.

This produces a list of file paths (one per line) that were added, modified, or deleted across the last N commits. Blank lines separate commits.

---

## 3. Build Scope

1. **Remove** blank lines and duplicates — produce a unique, sorted file list
    > [!NOTE]
    > **Deleted files are included intentionally.** `git log --name-only` does not distinguish deleted files from modified or added ones. This is safe — `/update-plan` §0–§2 discovery resolves scope from the live filesystem (`list_dir`, `find_by_name`, `grep_search`), so deleted files fail to match any skill or project and are silently excluded during scope resolution.
2. **Validate** — if no files changed, report and stop:
   ```
   ℹ️ No files changed in the last <N> commit(s). Nothing to sync.
   ```
3. **Format** the scope string:
   ```
   files: <comma-separated file paths>
   ```
   Example: `files: .agent/skills/drn-hosting/SKILL.md, DRN.Framework/Hosting/Startup.cs`

---

## 4. Report & Delegate

Present the detected scope to the user:

```markdown
## 🔍 update-last: Detected Changes

**Commits inspected**: <N>
**Changed files** (<count>):
- <file1>
- <file2>
- ...

**Scope**: `files: <comma-separated>`

Delegating to `/update files: <scope>` …
```

Then **load and execute** the `/update` workflow:

```
view_file .agent/workflows/update.md
```

Pass `files: <comma-separated file paths>` as the scope argument. The `/update` orchestrator's freeform scope resolution (`update-plan.md §0`) will interpret the file list, map it to affected skills/projects/stages, and confirm with the user before proceeding.

---

## Operational Notes

| Property | Detail |
|----------|--------|
| **Read-only git** | Only reads history — no checkout, reset, or fetch |
| **Freeform scope** | Reuses `/update`'s existing freeform resolution — no new scope logic |
| **Fail-fast** | Stops immediately on invalid input or empty changeset |
| **Composable** | Can be chained after `git pull` or CI triggers |
