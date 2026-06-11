---
description: Commit staged changes and polish non-pushed commit messages to comply with basic-git-conventions — never pushes, only local history rewrites
---

## 1. Role & Safety
**Commit Message Editor**: Commit staged changes and polish non-pushed commits.
> [!CAUTION]
> **NEVER execute `git push`** (including `--force`). Only perform local commits and rewrites (`--amend` or `rebase -i`). Refuse push requests.

---

## 2. Load Skills
```text
view_file .agent/skills/basic-git-conventions/SKILL.md
```
*Single source of truth for format rules.*

---

## 3. Commit Staged Changes
Check staged changes:
```bash
git diff --cached --stat
```
- **Nothing staged**: Skip to §4.
- **Staged changes**: Analyze diff, draft compliant message, get user confirmation, then commit:
  ```bash
  git commit -m "<compliant message>"
  ```
  If changes span multiple scopes, split them: unstage (`git reset HEAD <files>`), commit the rest, then stage and commit the remainder.

---

## 4. Resolve Scope & Detect Non-Pushed Commits
| Invocation | Scope |
|---|---|
| `/commit-polish` | All non-pushed commits |
| `/commit-polish N` | Last N non-pushed commits |

Detect non-pushed range (stop if empty, limit to N if provided):
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

---

## 5. Analyze Messages
Evaluate each commit message against conventions. If message is vague, inspect the diff:
```bash
git show <sha> --stat   # file overview
git show <sha>          # full diff (if --stat is insufficient)
```

---

## 6. Preview & Confirm
Present changes and await explicit user approval before rewriting:
```markdown
## Commit Message Changes
| # | SHA | Current Message | Proposed Message | Violations Fixed |
|---|-----|-----------------|------------------|------------------|
| 1 | `abc1234` | `fixed stuff` | `fix(Utils): resolve null ref in attribute scanner` | format, type, scope, mood |
```

---

## 7. Rewrite
**Single commit (HEAD)**:
```bash
git commit --amend -m "<new message>"
```

**Multiple commits** (verify tree is clean; stash if dirty):
```bash
# Mark target commits for reword (MacOS/Linux portable sed)
GIT_SEQUENCE_EDITOR="sed -i.bak \"s/^pick ${SHA}/reword ${SHA}/\" \"\$1\" && rm -f \"\$1.bak\"" git rebase -i <base>

# Rewriting loop using temporary file
MSGFILE=$(mktemp)
cat > "$MSGFILE" <<'COMMITMSG'
<type>(<scope>): <description>
COMMITMSG
GIT_EDITOR="cp \"$MSGFILE\"" git rebase --continue
rm -f "$MSGFILE"
```
*Abort rebase on conflict (`git rebase --abort`) and report.*

---

## 8. Verify & Report
Confirm compliance:
```markdown
## Results
| # | SHA (new) | Message | Status |
|---|-----------|---------|--------|
```
**Push status**: ⛔ Not pushed (by design)

---

## Related
- `basic-git-conventions` · `/review`
