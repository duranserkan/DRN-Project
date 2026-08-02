---
description: Recommend and apply polished commit messages for single or multiple verified-unpublished commits after user approval; never push or rewrite published history
---

# Commit Polish

> See also: [Operating Model](./_shared/workflow-operating-model.md)
> **Estimated context: ~1.0K tokens**

## 1. Mandate

Act as Commit Message Editor.

- Commit staged changes only after confirmation; otherwise recommend messages for verified-unpublished commits using `basic-git-conventions`.
- Reorganize mixed staged work into coherent commits. Reorganize history only when unpublished `HEAD` mixes concerns; never reorganize an earlier candidate.
- Preview messages and reorganization plans before mutation. Apply only explicitly approved changes.
- Before reorganizing `HEAD`, snapshot it and prove final tree equality and an empty diff.
- Never run `git push`, including `--force`.
- Support `--amend`, atomic `commit-tree` chains, and `HEAD` reorganization. Run the Startup Gate and load `basic-git-conventions`.

Refuse push requests. Stop before any rewrite that could touch published history.

## 2. Commit Staged Changes

Check staged work:

```bash
git diff --cached --stat
```

- If nothing is staged, continue to non-pushed commit detection.
- If changes are staged, inspect `git diff --cached --binary --full-index --no-ext-diff --no-textconv`. Persist `HEAD`, staged tree and paths, exact author/committer metadata, organization/order, message, and SHA-256 digests of the staged diff, plan, and raw `git status --porcelain=v1 -z --untracked-files=all`. Present the staged-diff and plan digests for approval.
- Bind approval to one staged-diff and plan digest. When reorganizing staged work, prepare and approve each coherent commit after its exact changes are staged.
- Treat each commit as a transaction. Create an empty hooks directory and exact message file. Recheck `HEAD`, status, index tree, paths, and diff/plan digests; abort on mismatch. With approved metadata, create the candidate without moving a ref via `git -c core.hooksPath="$empty_hooks_dir" commit-tree`. Recheck protected state and verify its raw tree, parent, message, author, and committer. Then atomically run `git update-ref HEAD "$candidate_sha" "$approved_head"`. Verify `HEAD` and planned status; CAS-rollback on mismatch. Clean up on every exit. Never use `git commit` here; any future path must isolate hooks and use `--no-verify --cleanup=verbatim`.
- If staged files span unrelated scopes, stage and separately approve each scope before committing it.

## 3. Select Rewrite Candidates

| Invocation | Scope |
|---|---|
| `/commit-polish` or `/commit-polish 1` | `HEAD` only (single commit) |
| `/commit-polish N` (`N > 1`) | Last N non-pushed commits (`HEAD~N..HEAD`) |
| `/commit-polish <range>` (e.g. `base..HEAD`) | A contiguous unpublished ancestry suffix ending at `HEAD` |

Before treating commits as unpublished:

1. Require a clean worktree and index (or commit staged changes first per Section 2).
2. Resolve candidates oldest-to-newest. Require a non-empty, duplicate-free linear chain ending at recorded `HEAD`; reject gaps, side branches, merges, or another tip.
3. Establish complete publication coverage for every configured publication remote. Require a successful `git ls-remote --heads --tags "$remote"` and an authorized complete fetch of all heads and tags; verify the fetched refs match that query. Abort when the remote set is unknown or coverage is missing, stale, or incomplete. Then record the exact remote-tracking and tag ref/object snapshot and its raw-byte SHA-256.
4. Record the worktree and index snapshots. Require `git rev-parse --is-shallow-repository` to output `false` and `git replace --list` to output nothing. Abort before ancestry or reachability checks otherwise.
5. For **each** candidate, query reachability from all recorded remote-tracking refs and tags; any result blocks rewriting. Checking only `HEAD` is insufficient.
6. Record each candidate's SHA, tree, parents, raw message, author metadata, and chain position. Reject `gpgsig`, `encoding`, or headers other than `tree`, `parent`, `author`, and `committer`.

```bash
git status --porcelain=v1 -z
git ls-remote --heads --tags "$remote"
git for-each-ref --format='%(refname) %(objectname)' refs/remotes
git for-each-ref --format='%(refname) %(objectname)' refs/tags
git rev-parse --is-shallow-repository
git replace --list
git for-each-ref --contains "$candidate_sha" --format='%(refname)' refs/remotes refs/tags
git rev-list --parents -n 1 "$candidate_sha"
```

Immediately before mutation, revalidate complete coverage, raw-history gates, topology, and per-candidate reachability. Absence from one selected base is not publication proof.

## 4. Analyze Messages and Formulate Recommendations

Compare each candidate commit message against `basic-git-conventions`.

If a message is vague or non-compliant, inspect the commit:

```bash
git show "$commit_sha" --stat
git show "$commit_sha"
```

Use the full diff only when the stat is insufficient.

When changes mix unrelated concerns, recommend reorganizing them into coherent commits (`type(scope): description`). Section 2 owns staged work. Section 6.C supports only current unpublished `HEAD`; stop for an earlier candidate.

Recommend each polished message or reorganized commit with its rationale.

## 5. Preview And Confirm Recommendations

Present message recommendations and reorganization plans, then wait for approval:

```markdown
## Commit Message Recommendations
| # | SHA | Current Message | Recommended Message | Rationale / Action |
|---|---|---|---|---|
| 1 | `abc1234` | `fixed stuff` | `fix(Utils): resolve null reference in scanner` | type, scope, imperative mood |
| 2a | `def5678` | `tests and docs` | `test(Utils): add scanner coverage` | Reorganize 1/2 |
| 2b | `def5678` | `tests and docs` | `docs(Utils): document scanner configuration` | Reorganize 2/2 |
```

Include clear notice:
> **Notice**: No commit messages have been modified yet. These are recommendations for single or multiple commits. Please review and confirm to apply message changes.

Do not rewrite until approval. For an earlier commit, disclose every descendant whose SHA changes through `HEAD`; approval must cover the complete chain. Approved replacement messages may cover a subset, but reconstruct each unchanged descendant with its raw message. Never silently rewrite collateral commits.

## 6. Apply Approved Message Changes

Apply only approved updates. For all rewrites, use an empty temporary hooks directory via `core.hooksPath="$empty_hooks_dir"` and clean up on every exit. Amend also uses `--no-verify --no-post-rewrite`.

### A. Single Commit Rewrite (`HEAD` only)

1. Write the exact approved message to a temporary file `$message_file`.
2. Re-verify the recorded `HEAD`, tree, ordered parents, clean status, unchanged remote-tracking snapshot, and per-candidate reachability.
3. Run:

   ```bash
   git -c core.hooksPath="$empty_hooks_dir" commit --amend --no-verify --no-post-rewrite --cleanup=verbatim --file "$message_file"
   ```

4. Verify the amended tree and ordered parents equal the originals, the raw message equals the approved bytes, and status remains clean.
5. On failure or mismatch, execute rollback with compare-and-swap protection:

   ```bash
   git -c core.hooksPath="$empty_hooks_dir" update-ref -m "rollback failed commit-polish amend" HEAD "$original_commit_sha" "$amended_commit_sha"
   ```

### B. Multi-Commit Rewrite (`N > 1` or range)

Let $C_1 \dots C_k$ be the approved chain from the earliest change through validated `HEAD`, oldest-to-newest. Replace approved messages; retain every other raw message.

1. Repeat every Section 3 validation and require the recorded status and remote-ref snapshot to remain unchanged.
2. Initialize `$parent_sha_i` to the sole original parent of $C_1$, or empty for a root commit. For each $C_i$:
   - Write the approved replacement or recorded raw message to `$message_file_i`. Load the recorded tree and author metadata; use an unambiguous date such as `%aI`.
   - Create $C'_i$ with the assignments applied directly to the `git commit-tree` subprocess:

     ```bash
     parent_args=()
     [ -n "$parent_sha_i" ] && parent_args=(-p "$parent_sha_i")
     new_sha_i="$(GIT_AUTHOR_NAME="$author_name_i" \
       GIT_AUTHOR_EMAIL="$author_email_i" GIT_AUTHOR_DATE="$author_date_i" \
       git -c core.hooksPath="$empty_hooks_dir" commit-tree "$tree_sha_i" \
       "${parent_args[@]}" -F "$message_file_i")"
     parent_sha_i="$new_sha_i"
     ```

   - Verify its tree, raw message, author, and parent against the approved record. Abort on mismatch.
3. Verify $C'_k$ has the original top tree and the old-to-new mapping exactly covers the approved chain.
4. Immediately recheck the remote-ref snapshot and per-candidate reachability, then perform atomic ref update with compare-and-swap protection:

   ```bash
   git -c core.hooksPath="$empty_hooks_dir" update-ref -m "commit-polish multi-commit reword" HEAD "$new_sha_k" "$old_sha_k"
   ```

5. Verify `HEAD`, the full rewritten topology, every approved message and author, the top tree, and clean status.
6. On post-update mismatch, CAS-restore `HEAD` to `$old_sha_k`, expecting `$new_sha_k`; verify and stop. If CAS fails, stop without further mutation.
7. Clean up; retain any recovery ref needed after failed rollback.

### C. Non-Destructive `HEAD` Reorganization

Reorganize the current unpublished `HEAD` into $M$ coherent commits only with a clean worktree and index. Section 2 owns staged work; earlier candidates are unsupported.

1. Bind approval to each commit's patch/tree digest, parent, author, and message. Create a unique snapshot at recorded `HEAD`; abort on collision:

   ```bash
   old_head_sha="$(git rev-parse HEAD)"
   snapshot_branch="snapshot-commit-polish-${old_head_sha}"
   git branch "$snapshot_branch" "$old_head_sha"
   snapshot_sha="$(git rev-parse "$snapshot_branch^{commit}")"
   test "$snapshot_sha" = "$old_head_sha"
   ```

2. Build the approved chain from the original parent of `HEAD`, verifying every tree and author. Immediately before the atomic update, repeat Section 3 status, topology, remote-snapshot, and per-candidate reachability checks:

   ```bash
   git -c core.hooksPath="$empty_hooks_dir" update-ref -m "commit-polish reorganize" HEAD "$new_sha_M" "$old_head_sha"
   ```

3. Verify every patch/tree, parent, author, and message; require top-tree equality, an empty diff, and clean status:

   ```bash
   test "$(git rev-parse "$snapshot_sha^{tree}")" = "$(git rev-parse 'HEAD^{tree}')"
   git diff --no-ext-diff --no-textconv --exit-code "$snapshot_sha" HEAD
   git status --porcelain=v1 -z
   ```

4. On any mismatch, attempt CAS rollback:

   ```bash
   git -c core.hooksPath="$empty_hooks_dir" update-ref -m "rollback failed commit reorganization" HEAD "$snapshot_sha" "$new_sha_M"
   ```

5. Never delete the snapshot automatically. Retain it as a user-managed recovery ref. Report its refname, object ID, equality to `$old_head_sha`, and that cleanup requires separate authorization.

## 7. Verify And Report

Report final results:

```markdown
## Results
| Old SHA | New SHA | Approved Message | Tree Equal | Parents Equal | Status |
|---|---|---|---|---|---|
```

State: `Push status: not pushed by design`.

For a `HEAD` reorganization, also state:

```text
Recovery snapshot: <exact snapshot branch refname>
Snapshot SHA: <current snapshot object ID>
Expected original SHA: <old HEAD SHA>
Recovery status: <retained and verified | retained but changed>
Cleanup status: retained; user action required
```

## Related

- `basic-git-conventions`
- `/review`
