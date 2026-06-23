---
description: Commit staged changes and polish non-pushed commit messages; never push or rewrite pushed history
---

> See also: [Operating Model](./_shared/workflow-operating-model.md)
> **Estimated context: ~0.9K tokens**

## 1. Mandate

Act as Commit Message Editor.

- Commit only staged changes after user confirmation.
- Polish only non-pushed commit messages after explicit approval.
- Never run `git push`, including `--force`.
- Use only local commits, `--amend`, or interactive rebase.
- Run the shared Startup Gate; load `basic-git-conventions`.

Refuse push requests. Stop before any rewrite that could touch pushed history.

## 2. Commit Staged Changes

Check staged work:

```bash
git diff --cached --stat
```

- If nothing is staged, continue to non-pushed commit detection.
- If staged changes exist, inspect the diff, draft a compliant message, ask for confirmation, then run:

  ```bash
  git commit -m "<compliant message>"
  ```

- If staged files span unrelated scopes, split them before committing: unstage selected files, commit the first scope, then stage and commit the next scope.

## 3. Select Non-Pushed Commits

| Invocation | Scope |
|---|---|
| `/commit-polish` | All non-pushed commits |
| `/commit-polish N` | Last `N` non-pushed commits |

Find the base:

1. Prefer upstream `@{u}`.
2. If absent, use the repository profile's integration or release branch.
3. If the profile is silent, inspect remotes and choose the safest primary ref.

Run only the matching branch. If no upstream exists, set `base_ref` from the profile or discovered primary ref; if no safe base can be resolved, stop and ask.

```bash
upstream_ref=$(git rev-parse --abbrev-ref @{u} 2>/dev/null || true)
if [ -n "$upstream_ref" ]; then
  base_ref="$upstream_ref"
else
  git for-each-ref --format='%(refname:short)' refs/remotes
  base_ref="<profile-or-discovered-base-ref>"
fi

if [ -z "$base_ref" ] || [ "$base_ref" = "<profile-or-discovered-base-ref>" ] || ! git rev-parse --verify --quiet "$base_ref" >/dev/null; then
  echo "No safe non-pushed commit base found; stop and ask."
  exit 1
fi

git log "$base_ref"..HEAD --oneline --no-decorate
```

Stop if the range is empty. Limit to `N` when supplied.

## 4. Analyze Messages

Compare each message against `basic-git-conventions`.

If a message is vague, inspect the commit:

```bash
git show <sha> --stat
git show <sha>
```

Use the full diff only when the stat is insufficient.

## 5. Preview And Confirm

Show the rewrite plan and wait for explicit approval:

```markdown
## Commit Message Changes
| # | SHA | Current | Proposed | Fixes |
|---|---|---|---|---|
| 1 | `abc1234` | `fixed stuff` | `fix(Utils): resolve null reference in scanner` | type, scope, mood |
```

Do not rewrite until approved.

## 6. Rewrite

For `HEAD` only:

```bash
git commit --amend -m "<new message>"
```

For multiple commits:

1. Verify the tree is clean. If dirty, stop and ask before stash or rewrite.
2. Reword only approved commits.
3. Abort and report on conflict.

```bash
GIT_SEQUENCE_EDITOR="sed -i.bak \"s/^pick ${SHA}/reword ${SHA}/\" \"\$1\" && rm -f \"\$1.bak\"" git rebase -i <base>

MSGFILE=$(mktemp)
cat > "$MSGFILE" <<'COMMITMSG'
<type>(<scope>): <description>
COMMITMSG
GIT_EDITOR="cp \"$MSGFILE\"" git rebase --continue
rm -f "$MSGFILE"
```

```bash
git rebase --abort
```

## 7. Verify And Report

Report final messages:

```markdown
## Results
| # | SHA (new) | Message | Status |
|---|---|---|---|
```

State: `Push status: not pushed by design`.

## Related

- `basic-git-conventions`
- `/review`
