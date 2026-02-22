---
name: commit-polish
description: Commit staged changes and polish non-pushed commit messages to comply with basic-git-conventions — never pushes, only local history rewrites
last-updated: 2026-02-22
tokens: ~1.5K
---

## 1. Role & Safety

**Commit Message Editor** — commit staged changes and polish non-pushed commit messages per `basic-git-conventions`.

> [!CAUTION]
> **[Important] rule: NEVER execute `git push`** (including `--force` and `--force-with-lease`) — this workflow only performs local commits and history rewrites (`--amend` / `rebase -i`). Refuse any request to push.

---

## 2. Load Skills

```text
view_file .agent/skills/basic-git-conventions/SKILL.md
```

Single source of truth for commit message format — do not duplicate criteria here.

---

## 3. Commit Staged Changes

Check for staged changes:

```bash
git diff --cached --stat
```

- **Nothing staged** → skip to §4.
- **Changes staged** → analyze the diff to infer type, scope, and description per `basic-git-conventions` rules. Draft a compliant commit message, present it for user confirmation, then commit:

```bash
git commit -m "<compliant message>"
```

> [!IMPORTANT]
> The drafted message must comply with all `basic-git-conventions` rules from the start — it goes through the same analysis as §5.
> If the user modifies the draft, re-validate the edited message against all rules before committing.
> If staged changes span unrelated scopes (e.g., files in `Utils` and `Hosting`), split into multiple commits — one per scope:
> 1. `git reset HEAD <files-for-second-scope>` — unstage the unrelated files
> 2. Commit the remaining staged files
> 3. `git add <files-for-second-scope>` — re-stage and commit the next scope

---

## 4. Resolve Scope & Detect Non-Pushed Commits

| Invocation | Scope |
|---|---|
| `/commit-polish` | All non-pushed commits |
| `/commit-polish N` | Last N non-pushed commits |

**Detection** — use first match, skip rest:

```bash
if git rev-parse --abbrev-ref @{u} 2>/dev/null; then
  git log @{u}..HEAD --oneline --no-decorate
elif git rev-parse --verify origin/develop 2>/dev/null; then
  git log origin/develop..HEAD --oneline --no-decorate
elif git rev-parse --verify origin/master 2>/dev/null; then
  git log origin/master..HEAD --oneline --no-decorate
else
  echo "No upstream reference found"
fi
```

- **No non-pushed commits** → inform user and stop.
- **Scope argument `N`** → limit to last N from the detected list.

---

## 5. Analyze Messages

For each non-pushed commit, evaluate against `basic-git-conventions` §Commit Message Format: format, type, scope, length (≤ 72), case, punctuation, mood, body.

Flag violations and draft an improved message.

**Context enrichment** — if the original message is vague or generic, read the commit diff to infer accurate type, scope, and description:

```bash
git show <sha> --stat   # file overview — always start here
git show <sha>          # full diff only when --stat is insufficient to infer type/scope
```

---

## 6. Preview & Confirm

Present a table of proposed changes. **Wait for explicit user approval** before rewriting.

```markdown
## Commit Message Changes

| # | SHA | Current Message | Proposed Message | Violations Fixed |
|---|-----|-----------------|------------------|------------------|
| 1 | `abc1234` | `fixed stuff` | `fix(Utils): resolve null ref in attribute scanner` | format, type, scope, mood |
| 2 | `def5678` | `feat(Utils): add new feature.` | `feat(Utils): add new feature` | trailing period |

**Action**: amend / rebase-reword (never push)
```

- Proceed only on unambiguous user approval. Questions or qualifiers require further dialogue.

---

## 7. Rewrite

After confirmation:

**Single commit (HEAD only)**:
```bash
git commit --amend -m "<new message>"
```

**Multiple commits**:

> [!IMPORTANT]
> Before rebase, verify working tree is clean (`git status --porcelain`). If dirty → stash first, pop after.

```bash
# 1. Mark target commits for reword
#    Use FULL SHA (or --abbrev=12+) to avoid prefix collision
#    Portable sed: -i.bak works on both macOS and Linux
#    Double-quote the sed pattern so ${SHA} expands; scope .bak cleanup to "$1.bak"
GIT_SEQUENCE_EDITOR="sed -i.bak \"s/^pick ${SHA}/reword ${SHA}/\" \"\$1\" && rm -f \"\$1.bak\"" git rebase -i <base>

# 2. For each reworded commit, write its message to a temp file
#    then let git use it as the editor
MSGFILE=$(mktemp)

# --- Loop: repeat this block for each reworded commit ---
cat > "$MSGFILE" <<'COMMITMSG'
<type>(<scope>): <description>

<optional body — explains why>
COMMITMSG
# Git invokes GIT_EDITOR with the commit-msg file as $1,
# so `cp "$MSGFILE"` becomes `cp "$MSGFILE" <commit-msg-file>`
GIT_EDITOR="cp \"$MSGFILE\"" git rebase --continue
# --- End loop: update $MSGFILE content, then continue ---

rm -f "$MSGFILE"
```

> [!NOTE]
> `sed -i.bak` + scoped `rm` for macOS/Linux portability. `mktemp` avoids symlink race conditions. `cp` approach avoids quoting issues with apostrophes and supports multi-line bodies.

> If rebase hits a conflict → `git rebase --abort` to safely back out. Report to user before retrying.

---

## 8. Verify & Report

After rewriting:

1. Re-run `git log` on the affected range.
2. Confirm all messages now comply with `basic-git-conventions`.
3. Report results:

```markdown
## Results

| # | SHA (new) | Message | Status |
|---|-----------|---------|--------|
| 1 | `aaa1111` | `fix(Utils): resolve null ref in attribute scanner` | ✅ Compliant |
| 2 | `bbb2222` | `feat(Utils): add new feature` | ✅ Compliant |

**Commits rewritten**: N
**Push status**: ⛔ Not pushed (by design)
```

---

## Related Skills & Workflows

- `basic-git-conventions` — commit message format, branching model, PR workflow
- `/review` — run after polishing to validate overall change quality