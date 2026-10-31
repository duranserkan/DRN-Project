---
name: basic-git-conventions
description: "Git workflow conventions - GitFlow-inspired branching (integration→release→tag), commit message format, PR workflow (draft→review→squash), branch naming (feature/fix/chore/docs), repository-declared release tagging, and release notes management. Keywords: git, branching, commit-messages, pull-request, pr-workflow, release, tagging, versioning, gitflow, conventional-commits"
last-updated: 2026-10-29
difficulty: basic
tokens: ~1.5K
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

```
  tag <release-tag>       tag <release-tag>
          │                       │
──────────┼───────────────────────┼──── <release-branch>
          │            ↑          │
          │          merge        │
          │            │          │
──────────┼────────────┼──────────┼──── <integration-branch>
          │         ↑  ↑  ↑       │
          │         │  │  │       │
          │       feat fix chore  │
```

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
- **The integration branch must always build** — broken builds block the team.
- **Feature branches are short-lived** — merge or close within days
- **Delete branches after merge** — keep the repository clean

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
- **Imperative mood** — "add" not "added" or "adds"
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

Each published module or package maintains its own release notes when the repository profile or package metadata declares it:

```
<module-or-package>/
├── RELEASE-NOTES.md   # Per-package changelog
└── README.md          # Package documentation
```

### When To Update

Update only the affected module/package release notes when a change reaches consumers:

| Trigger | Examples |
|---------|----------|
| Public contract | APIs, DTOs, endpoints, events, CLI options, generated package metadata |
| Runtime behavior | Defaults, validation, security posture, auth, rate limits, errors, data/migration behavior |
| Fixes | Observable bug fixes, compatibility fixes, performance changes with user-facing effect |
| Dependencies or tooling | Breaking, security-relevant, runtime/container/default changes, or changed published artifacts |

Do not create release-note noise for internal-only refactors, tests, comments, agent-only docs, or routine dependency-only updates with no consumer-visible effect unless the repository profile declares a stricter release rule.

For version-aligned releases, leave unchanged module/package release notes untouched unless the repository profile explicitly requires an empty or consistency-only entry.

### Format
Use the repository profile or package metadata for the exact prefix, heading style, section names, and footer. If no local template is declared, use this portable default:

```markdown
## Version X.Y.Z

### Breaking Changes

- **Area**: Detail description.

### New Features

- **Feature Group**: Detail description.

### Changed

- **Area**: Detail description.

### Bug Fixes

- **Area**: Detail description.
```

---

## Related Skills
- [overview-github-actions.md](../overview-github-actions/SKILL.md) - CI/CD pipeline details
- [basic-code-review.md](../basic-code-review/SKILL.md) - Review standards
- [basic-documentation.md](../basic-documentation/SKILL.md) - Documentation standards
