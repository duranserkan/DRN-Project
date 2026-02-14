---
name: basic-git-conventions
description: Git workflow conventions - GitFlow-inspired branching (develop→master→tag), commit message format, PR workflow (draft→review→squash), branch naming (feature/fix/chore/docs), release tagging (v*.*.* and v*-preview*), and release notes management. Keywords: git, branching, commit-messages, pull-request, pr-workflow, release, tagging, versioning, gitflow, conventional-commits
---

# Git Conventions & Branching

> Branching model, commit messages, PR workflow, and release tagging for DRN-Project.

## When to Apply
- Creating new branches
- Writing commit messages
- Opening pull requests
- Preparing releases
- Managing version tags

---

## Branching Model

```
  tag v1.0.0        tag v1.1.0
       │                 │
───────┼─────────────────┼──── master (release-ready)
       │        ↑        │
       │     merge       │
       │        │        │
───────┼────────┼────────┼──── develop (integration)
       │     ↑  ↑  ↑     │
       │     │  │  │     │
       │   feat fix chore│
```

| Branch | Purpose | Merges Into |
|--------|---------|-------------|
| `master` | Release-ready code | Tagged for release |
| `develop` | Integration branch | `master` |
| `feature/*` | New functionality | `develop` |
| `fix/*` | Bug fixes | `develop` |
| `chore/*` | Maintenance, refactoring | `develop` |
| `docs/*` | Documentation only | `develop` |

### Rules
- **Never push directly to `master`** — always via PR from `develop`
- **`develop` must always build** — broken builds block the team
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
Use the affected package or area:
- `SharedKernel`, `Utils`, `Hosting`, `EntityFramework`, `Testing`
- `Sample`, `Nexus`
- `ci`, `docker`, `docs`, `skills`

### Examples
```
feat(Sample): add user profile management page
fix(EntityFramework): resolve connection pool exhaustion under load
refactor(Utils): simplify attribute scanning with cached reflection
test(Integration): add concurrency test for QAContext
chore(ci): upgrade actions/checkout to v6
security(Hosting): strengthen CSP for font-src directive
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
feat(Utils): add cancellation token propagation to HttpClientFactory
```

### Merge Strategy
- **Squash merge** into `develop` — keeps history clean
- **Merge commit** into `master` — preserves the merge point

---

## Release Tagging

### Stable Releases
```bash
# Tag format: v{major}.{minor}.{patch}
git tag v1.2.3
git push origin v1.2.3
# Triggers: release.yml → NuGet publish + Docker push
```

### Preview Releases
```bash
# Tag format: v{major}.{minor}.{patch}-preview{N}
git tag v1.3.0-preview1
git push origin v1.3.0-preview1
# Triggers: release-preview.yml → NuGet preview publish
```

### Versioning Rules
- **Major**: Breaking API changes
- **Minor**: New features, backward compatible
- **Patch**: Bug fixes, backward compatible
- **Preview**: Pre-release testing

---

## Release Notes

Each framework package maintains its own release notes:

```
DRN.Framework.{Package}/
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
- [github-actions.md](../github-actions/SKILL.md) - CI/CD pipeline details
- [basic-code-review.md](../basic-code-review/SKILL.md) - Review standards
- [basic-documentation.md](../basic-documentation/SKILL.md) - Documentation standards
