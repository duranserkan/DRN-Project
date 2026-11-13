---
name: basic-git-conventions
description: Use when preparing authorized branches, commits, pull requests, release tags, or release notes.
last-updated: 2026-11-13
difficulty: basic
---

# Git Conventions & Branching

> Branching model, commit messages, PR workflow, and release tagging. Apply repository-profile branch and release rules when present.
> Branch names, tag patterns, package scopes, and release automation below are portable defaults or examples unless the repository profile declares stricter rules.

## When to Apply
- Creating new branches
- Writing commit messages
- Opening pull requests
- Preparing releases
- Managing version tags

---

## Branching Model

| Branch | Purpose | Merges Into |
|--------|---------|-------------|
| `<release-branch>` | Release-ready code | Tagged for release |
| `<integration-branch>` | Integration branch | `<release-branch>` |
| `feature/*` | New functionality | `<integration-branch>` |
| `fix/*` | Bug fixes | `<integration-branch>` |
| `chore/*` | Maintenance, refactoring | `<integration-branch>` |
| `docs/*` | Documentation only | `<integration-branch>` |

### Rules
- **Never push directly to the protected release branch** declared by the repository profile.
- Keep the integration branch buildable. This requirement does not authorize local execution.
- Keep feature branches short-lived.
- Delete merged branches only when authorized.

---

## Commit Message Format

Use structured, descriptive messages:

```
<type>(<scope>): <description>

[optional body]
[optional footer]
```

### Types
| Type | When to Use |
|------|------------|
| `feat` | New feature or capability |
| `fix` | Bug fix |
| `refactor` | Code restructuring without behavior change |
| `test` | Adding or updating tests |
| `docs` | Documentation changes |
| `chore` | Build, CI, dependency updates |
| `perf` | Performance improvement (with proof) |
| `security` | Security fix or improvement |

### Scope
Use the affected package or area from the repository profile, package metadata, or changed paths:
- `<package-or-module>`, `<application>`, `<bounded-context>`
- `ci`, `docker`, `docs`, `skills`

### Examples
```
feat(App): add user profile management page
fix(Persistence): resolve connection pool exhaustion under load
refactor(Core): simplify attribute scanning with cached reflection
test(Integration): add concurrency test for repository queries
chore(ci): upgrade actions/checkout to v6
security(Web): strengthen CSP for font-src directive
docs(skills): add security development checklist
```

### Rules
- Do not stage changes, commit, or amend commits unless the user explicitly authorizes the action. Keep changes unstaged for review by default. Preserve any pre-existing staged changes.
- Implementation or review requests do not authorize staging or committing. Stage only explicitly authorized paths or hunks when staging is requested.
- **Subject line ≤ 72 characters**
- **Start with lowercase** after the colon
- **No period** at the end of the subject
- **Imperative mood**: "add" not "added" or "adds"
- **Body explains why**, not what (the diff shows what)

---

## Pull Request Workflow

### Lifecycle
```
Draft PR → Self-Review → Ready for Review → Review → Squash Merge
```

### PR Checklist
- [ ] Branch is up-to-date with target
- [ ] All CI checks pass (build + test)
- [ ] Required human and automated review feedback addressed
- [ ] Self-reviewed changes diff
- [ ] No unrelated changes included
- [ ] Tests added for new functionality

### PR Title
Follow the commit message format:
```
feat(Networking): add cancellation token propagation to HttpClientFactory
```

### Merge Strategy
- **Squash merge** into the profile-declared integration branch when that is the local policy.
- **Merge commit** into the profile-declared release branch when that is the local policy.

---

## Release Tagging

Resolve tag formats, the release commit, and the publishing remote from the repository profile or release automation before substituting the placeholders below. Tag creation and pushing require explicit authorization.

### Stable Releases
```bash
git tag <release-tag> <release-commit>
git push <publishing-remote> <release-tag>
# Triggers: repository-defined release automation
```

### Preview Releases
```bash
git tag <preview-tag> <release-commit>
git push <publishing-remote> <preview-tag>
# Triggers: repository-defined preview release automation
```

### Versioning Rules
- **Major**: Breaking API changes
- **Minor**: New features, backward compatible
- **Patch**: Bug fixes, backward compatible
- **Preview**: Pre-release testing. Use the repository's prerelease identifier and numbering convention.

---

## Release Notes

Use the module/package release-note owner declared by the repository profile or package metadata.

### When To Update

Apply the profile and [documentation triggers](../basic-documentation/SKILL.md#release-notesmd). Update affected modules only. Record "not required" when no trigger applies.

### Format
Follow the local prefix, headings, sections, and footer. Without a local template, use a version heading and applicable Breaking Changes, New Features, Changed, or Bug Fixes sections.

---

## Related Skills
- [overview-github-actions.md](../overview-github-actions/SKILL.md) - CI/CD pipeline details
- [basic-code-review.md](../basic-code-review/SKILL.md) - Review standards
- [basic-documentation.md](../basic-documentation/SKILL.md) - Documentation standards
