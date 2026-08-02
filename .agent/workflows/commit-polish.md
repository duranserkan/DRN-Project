---
description: Recommend and apply polished commit messages for single or multiple verified-unpublished commits after user approval; never push or rewrite published history
---

> See also: [Operating Model](./_shared/workflow-operating-model.md)
> **Estimated context: ~1.0K tokens**

## 1. Mandate

Act as Commit Message Editor.

- Commit staged changes only after confirmation; otherwise recommend polished messages for verified-unpublished commits using `basic-git-conventions`.
- Recommend reorganizing mixed staged work into coherent commits. Reorganize existing history only when the current unpublished `HEAD` contains mixed concerns; never reorganize an earlier candidate.
- Preview messages and reorganization plans before mutation. Apply only explicitly approved changes.
- Before reorganizing `HEAD`, create a snapshot branch and prove final tree equality with an empty `git diff <snapshot-branch> HEAD`.
- Never run `git push`, including `--force`.
- Support `--amend`, atomic `commit-tree` chains, and non-destructive `HEAD` reorganization. Run the Startup Gate and load `basic-git-conventions`.

Refuse push requests. Stop before any rewrite that could touch published history.

## 2. Commit Staged Changes

Check staged work:

```bash
git diff --cached --stat
```

- If nothing is staged, continue to non-pushed commit detection.
- If staged changes exist, inspect `git diff --cached --binary --full-index --no-ext-diff --no-textconv`. Persist a plan containing `HEAD`, staged-diff SHA-256, raw-byte SHA-256 of `git status --porcelain=v1 -z --untracked-files=all`, staged paths, organization/order, and proposed message. Present the staged-diff and plan digests for approval.
- Bind approval to one staged-diff and plan digest. When reorganizing staged work, prepare and approve each coherent commit after its exact changes are staged.
- Immediately before every approved commit mutation, recheck `HEAD`, recompute the NUL-delimited status and staged-diff digests, regenerate the exact commit plan from those current values, and compare every digest with the approved plan. Abort on any worktree, index, path, plan, or digest change. Then write the exact approved message to a temporary file and run:

  ```bash
  git commit --file "$message_file"
  ```

- If staged files span unrelated scopes, reorganize them: stage the first approved scope, commit it, then prepare and separately approve each remaining scope.

## 3. Select Rewrite Candidates

| Invocation | Scope |
|---|---|
| `/commit-polish` or `/commit-polish 1` | `HEAD` only (single commit) |
| `/commit-polish N` (`N > 1`) | Last N non-pushed commits (`HEAD~N..HEAD`) |
| `/commit-polish <range>` (e.g. `base..HEAD`) | A contiguous unpublished ancestry suffix ending at `HEAD` |

Before treating commits as unpublished:

1. Require a clean worktree and index (or commit staged changes first per Section 2).
2. Resolve the candidate SHAs oldest-to-newest. Require a non-empty, duplicate-free list whose newest SHA is the recorded `HEAD`; every candidate must have at most one parent, and each candidate after the first must name the preceding candidate as its sole parent. Reject ranges with gaps, side branches, merges, or a tip other than `HEAD`.
3. Ensure remote-tracking refs are current. If freshness is not established, ask the user to fetch or explicitly authorize a fetch; never infer freshness from a range. Record the exact remote-tracking refname/objectname snapshot and its raw-byte SHA-256.
4. For **each** candidate SHA, run the reachability query below and stop if it returns any remote-tracking ref or tag. Checking only `HEAD` is insufficient because an older candidate may already be published.
5. Record each candidate's commit SHA, tree SHA, ordered parents, raw message, author name/email/date, and its position in the validated chain. Inspect the raw commit headers and stop if a candidate contains `gpgsig`, `encoding`, or any header other than `tree`, `parent`, `author`, and `committer`; `commit-tree` must not silently discard signatures or unsupported metadata.

```bash
git status --porcelain=v1 -z
git for-each-ref --format='%(refname) %(objectname)' refs/remotes
git for-each-ref --contains "$candidate_sha" --format='%(refname)' refs/remotes refs/tags
git rev-list --parents -n 1 "$candidate_sha"
```

Repeat topology and per-candidate reachability validation immediately before mutation. Any reachability output blocks rewriting. Absence from one selected base is not publication proof.

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

Do not rewrite until explicit approval. A preview for an earlier commit must disclose every descendant whose SHA will change through `HEAD`, even when its message stays unchanged, and approval must cover that complete rewrite chain. Distinguish the complete rewrite chain from the message-change subset: the user may approve new messages for a subset, but must approve reconstruction of every unchanged descendant with its original raw message. Never silently rewrite collateral commits.

## 6. Apply Approved Message Changes

Execute commit message updates only after the user explicitly approves recommendations.

For all rewrites, create an empty temporary hooks directory and use `core.hooksPath="$empty_hooks_dir"`. For `git commit --amend`, also use `--no-verify --no-post-rewrite`. Remove temporary files and directories on every exit.

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

Let $C_1 \dots C_k$ be the complete approved rewrite chain from the earliest message change through the validated `HEAD`, ordered oldest-to-newest. The message-change subset receives approved replacement messages; every other $C_i$ retains its recorded raw message.

1. Repeat every Section 3 validation and require the recorded status and remote-ref snapshot to remain unchanged.
2. Initialize `$parent_sha_i` to the sole original parent of $C_1$, or empty for a root commit. For each $C_i$:
   - Write the exact approved replacement message to `$message_file_i` when $C_i$ is in the message-change subset; otherwise write its recorded raw message. Load its recorded tree and author metadata, using an unambiguous Git date format such as `%aI`.
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
   - Verify its tree, expected replacement-or-original raw message, author name/email/date, and parent (`C'_{i-1}` after the first) against the approved record. Abort before ref mutation on any mismatch.
3. Verify $C'_k$ has the original top tree and that the complete old-to-new mapping contains exactly every commit in the approved rewrite chain, including unchanged descendants.
4. Immediately recheck the remote-ref snapshot and per-candidate reachability, then perform atomic ref update with compare-and-swap protection:
   ```bash
   git -c core.hooksPath="$empty_hooks_dir" update-ref -m "commit-polish multi-commit reword" HEAD "$new_sha_k" "$old_sha_k"
   ```
5. Verify `HEAD`, the full rewritten topology, every approved message and author, the top tree, and clean status.
6. On mismatch before `update-ref`, no ref changed. On post-update mismatch, CAS-restore `HEAD` to `$old_sha_k` with expected old value `$new_sha_k`; verify restoration and stop. If CAS fails, stop without further mutation.
7. Clean up all temporary files and directories; retain any recovery ref needed after a failed rollback.

### C. Non-Destructive `HEAD` Reorganization

Reorganize the current unpublished `HEAD` into $M$ coherent commits only with a clean worktree and index. Section 2 owns staged work; earlier candidates are unsupported.

1. Bind approval to each ordered commit's patch/tree digest, parent, author metadata, and message. Create a unique snapshot branch at recorded `HEAD`; abort on collision:
   ```bash
   old_head_sha="$(git rev-parse HEAD)"
   snapshot_branch="snapshot-commit-polish-${old_head_sha}"
   git branch "$snapshot_branch" HEAD
   ```
2. Build the approved chain from the original parent of `HEAD`, verifying every tree and author. Immediately before the atomic update, repeat Section 3 status, topology, remote-snapshot, and per-candidate reachability checks:
   ```bash
   git -c core.hooksPath="$empty_hooks_dir" update-ref -m "commit-polish reorganize" HEAD "$new_sha_M" "$old_head_sha"
   ```
3. Verify every patch/tree, parent, author, and message; require top-tree equality, an empty diff, and clean status:
   ```bash
   git diff --exit-code "$snapshot_branch" HEAD
   git status --porcelain=v1 -z
   ```
4. On any mismatch, attempt CAS rollback:
   ```bash
   git -c core.hooksPath="$empty_hooks_dir" update-ref -m "rollback failed commit reorganization" HEAD "$snapshot_branch" "$new_sha_M"
   ```
5. Never delete the snapshot branch automatically. Retain it after success, rollback, or failure as a user-managed recovery ref. Verify and report its exact refname and object ID, whether it still points to `$old_head_sha`, and that cleanup requires a separate user-authorized action.

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
