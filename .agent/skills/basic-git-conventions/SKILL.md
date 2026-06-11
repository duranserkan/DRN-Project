---
name: basic-git-conventions
description: "Git workflow conventions - GitFlow-inspired branching (integration→release→tag), commit message format, PR workflow (draft→review→squash), branch naming (feature/fix/chore/docs), repository-declared release tagging, and release notes management. Keywords: git, branching, commit-messages, pull-request, pr-workflow, release, tagging, versioning, gitflow, conventional-commits"
last-updated: 2026-06-12
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
- [ ] CodeRabbit review addressed
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

### Stable Releases
```bash
# Example tag format: release/v{major}.{minor}.{patch}
git tag release/v1.2.3
git push origin release/v1.2.3
# Triggers: repository-defined release automation
```

### Preview Releases
```bash
# Example tag format: release/v{major}.{minor}.{patch}-preview{NNN}
git tag release/v1.3.0-preview001
git push origin release/v1.3.0-preview001
# Triggers: repository-defined preview release automation
```

### Versioning Rules
- **Major**: Breaking API changes
- **Minor**: New features, backward compatible
- **Patch**: Bug fixes, backward compatible
- **Preview**: Pre-release testing; use three digits (`preview001`, `preview002`) for natural tag ordering

---

## Release Notes

Each published module or package maintains its own release notes when the repository profile or package metadata declares it:

```
<module-or-package>/
├── RELEASE-NOTES.md   # Per-package changelog
├── README.md          # Package documentation
└── PACKAGE-DESCRIPTION # NuGet short description
```

### Format
```markdown
## v1.2.3

### Added
- New feature description

### Fixed
- Bug fix description

### Changed
- Behavior change description

### Security
- Security improvement description
```

---

## Related Skills
- [overview-github-actions.md](../overview-github-actions/SKILL.md) - CI/CD pipeline details
- [basic-code-review.md](../basic-code-review/SKILL.md) - Review standards
- [basic-documentation.md](../basic-documentation/SKILL.md) - Documentation standards
