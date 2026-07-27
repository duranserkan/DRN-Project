---
description: Commit staged changes and polish a verified-unpublished HEAD message; never push or rewrite published history
---

> See also: [Operating Model](./_shared/workflow-operating-model.md)
> **Estimated context: ~0.9K tokens**

## 1. Mandate

Act as Commit Message Editor.

- Commit only staged changes after user confirmation.
- Polish only a verified-unpublished, non-merge `HEAD` message after explicit approval.
- Never run `git push`, including `--force`.
- Use `--amend` only; multi-commit rewriting is blocked by Section 6.
- Run the shared Startup Gate; load `basic-git-conventions`.

Refuse push requests. Stop before any rewrite that could touch pushed history.

## 2. Commit Staged Changes

Check staged work:

```bash
git diff --cached --stat
```

- If nothing is staged, continue to non-pushed commit detection.
- If staged changes exist, inspect the exact raw bytes from `git diff --cached --binary --full-index --no-ext-diff --no-textconv`. Persist a commit plan with the current `HEAD`, exact staged-diff SHA-256, raw-byte SHA-256 of `git status --porcelain=v1 -z --untracked-files=all`, staged paths, split boundary and order, and proposed message; compute the plan's raw-byte SHA-256 and present the staged-diff and commit-plan digests for approval.
- Bind approval to one staged-diff and commit-plan digest. Each split commit requires its own plan and separate approval after its exact changes are staged.
- Immediately before every approved commit mutation, recheck `HEAD`, recompute the NUL-delimited status and staged-diff digests, regenerate the exact commit plan from those current values, and compare every digest with the approved plan. Abort on any worktree, index, path, plan, or digest change. Then write the exact approved message to a temporary file and run:

  ```bash
  git commit --file "$message_file"
  ```

- If staged files span unrelated scopes, split them before committing: stage only the first approved scope, commit it, then prepare and separately approve each remaining scope.

## 3. Select Rewrite Candidate

| Invocation | Scope |
|---|---|
| `/commit-polish` or `/commit-polish 1` | `HEAD` only |
| `/commit-polish N`, `N > 1` | Stop under the multi-commit block in Section 6 |

Before treating `HEAD` as unpublished:

1. Require a clean worktree and index.
2. Stop if `HEAD` is a merge commit.
3. Ensure remote-tracking refs are current. If freshness is not established, ask the user to fetch or explicitly authorize a fetch; never infer freshness from `base..HEAD`. Record the exact remote-tracking refname/objectname snapshot and its raw-byte SHA-256.
4. Stop if `HEAD` is reachable from any remote-tracking ref or tag.
5. Record the original commit SHA and tree SHA.

```bash
git status --porcelain=v1 -z
git rev-list --parents -n 1 HEAD
git for-each-ref --format='%(refname) %(objectname)' refs/remotes
git for-each-ref --contains HEAD --format='%(refname)' refs/remotes refs/tags
git rev-parse HEAD
git rev-parse HEAD^{tree}
```

Any output from the reachability command blocks rewriting. Absence from one selected base is not publication proof.

## 4. Analyze Messages

Compare the `HEAD` message against `basic-git-conventions`.

If a message is vague, inspect the commit:

```bash
git show "$commit_sha" --stat
git show "$commit_sha"
```

Use the full diff only when the stat is insufficient.

## 5. Preview And Confirm

Show the rewrite plan and wait for explicit approval:

```markdown
## Commit Message Changes
| SHA | Current | Proposed | Fixes |
|---|---|---|---|
| `abc1234` | `fixed stuff` | `fix(Utils): resolve null reference in scanner` | type, scope, mood |
```

Do not rewrite until approved.

## 6. Rewrite

Multi-commit rewriting is unsupported. Do not use interactive rebase until a separate reviewed specification defines all of:

- Proof that every rewritten commit is unreachable from every current remote-tracking ref and tag.
- Exact old-SHA-to-approved-message mapping across rewritten SHAs.
- Merge-commit handling and topology preservation.
- Pre/post tree and final topology equality checks.
- File-based message transport without shell interpolation.

For the approved `HEAD`-only rewrite:

1. Write the exact approved message to a temporary file through the platform file-write capability.
2. Immediately before mutation, recheck that `HEAD` and its tree SHA match the recorded values, the worktree and index are clean, the current remote-tracking-ref snapshot is unchanged and still fresh, and `HEAD` remains unreachable from every current remote-tracking ref and tag. Abort and require a new preview and approval on any mismatch.
3. Run `git commit --amend --file "$message_file"`; never interpolate the message into a shell command.
4. Remove the temporary file.
5. Stop and report if the resulting tree SHA differs from the recorded tree SHA.

```bash
git status --porcelain=v1 -z
git rev-parse HEAD
git rev-parse HEAD^{tree}
git for-each-ref --format='%(refname) %(objectname)' refs/remotes
git for-each-ref --contains HEAD --format='%(refname)' refs/remotes refs/tags
git commit --amend --file "$message_file"
git rev-parse HEAD^{tree}
```

## 7. Verify And Report

Report final messages:

```markdown
## Results
| Old SHA | New SHA | Message | Tree Equal | Status |
|---|---|---|---|---|
```

State: `Push status: not pushed by design`.

## Related

- `basic-git-conventions`
- `/review`
