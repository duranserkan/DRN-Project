---
name: overview-skill-index
description: Use when choosing which repository skills to load for a task, mapping work to skill families, finding related skills, or updating skill routing after skills change.
last-updated: 2026-06-12
difficulty: basic
tokens: ~1.4K
---

# Skill Cross-Reference Index

> Portable skill router. Read `.agent/repository-profile.md` first so profile-declared framework or repository overlays can refine generic routing.

## Selection Rules

1. Load the smallest set of skills that covers the task.
2. Prefer generic `basic-*`, `overview-*`, and `test-*` skills for portable process guidance.
3. Load framework-specific skills only when the repository profile, source code, or user request names that framework.
4. Load `frontend-*` skills only when the repository profile or filesystem declares the matching frontend convention.
5. If a referenced skill is missing after copying to another repository, skip it and rely on conventions/profile discovery.

## By Task

| Task | Skills to Load |
|------|----------------|
| Review a PR or diff | `basic-code-review` -> `basic-security-checklist` |
| Write documentation | `basic-documentation` -> `basic-documentation-diagrams` |
| Navigate repository | `overview-repository-structure` |
| Review architecture | `overview-ddd-architecture` when DDD applies |
| Modify CI/CD | `overview-github-actions` -> `basic-security-checklist` |
| Add unit tests | repository testing profile -> `test-unit` |
| Add integration tests | repository testing profile -> `test-integration` plus `test-integration-api` or `test-integration-db` |
| Run benchmarks | `test-performance` |
| Add frontend package | repository frontend profile -> `frontend-buildwww-packages` when `buildwww` applies |
| Modify Vite/buildwww | `frontend-buildwww-vite` when that convention applies |
| Add Razor UI | `frontend-razor-pages-shared` -> `frontend-razor-pages-navigation` -> `frontend-razor-accessors` |
| Add React mounted island | `frontend-buildwww-libraries` -> `frontend-buildwww-react` when that convention applies |
| Work as an AI agent | `basic-agentic-development` |
| Sync skills/workflows | `/update` workflow |

## Framework And Convention-Specific Families

The following families are framework- or convention-scoped and should be used only when present and relevant:

| Family | Trigger |
|--------|---------|
| `drn-*` | Repository uses DRN Framework or the user asks for DRN behavior |
| `overview-drn-*` | Repository profile declares DRN overview/testing conventions |
| `frontend-buildwww-*` | Repository profile or filesystem declares `buildwww` frontend convention |

## By Layer

| Concern | Portable Skills | Framework/Profile Overlay |
|---------|-----------------|----------------|
| Domain / architecture | `overview-ddd-architecture` | Framework/domain skill from profile |
| Hosting / API | `basic-security-checklist`, `test-integration-api` | Hosting skill from profile |
| Persistence | `test-integration-db` | ORM/framework skill from profile |
| Testing | `test-unit`, `test-integration`, `test-performance` | Framework/profile testing skill |
| Frontend | Detected `frontend-*` convention skill | Framework/web skill from profile |
| CI/CD | `overview-github-actions`, `basic-git-conventions` | Release/deployment profile |

## Keyword Index

Portable keywords should stay broad. Repository- or framework-specific terms belong in the profile-scoped index below and apply only when `.agent/repository-profile.md` declares the matching convention.

| Keyword | Skills |
|---------|--------|
| agentic / ai-agent | `basic-agentic-development` |
| architecture / ddd | `overview-ddd-architecture` |
| ci / github-actions / deployment | `overview-github-actions` |
| documentation / readme / release-notes | `basic-documentation`, `/documentation` |
| frontend / vite / buildwww | `frontend-buildwww-vite`, `frontend-buildwww-packages` |
| htmx / csp / nonce | `frontend-buildwww-libraries`, `basic-security-checklist` |
| react / islands | `frontend-buildwww-react` |
| repository / navigation | `overview-repository-structure` |
| security | `basic-security-checklist`, `basic-code-review` |
| testing / unit / integration | `test-unit`, `test-integration`, `test-integration-api`, `test-integration-db` |

### Profile-Scoped Keywords

Use these terms only when the repository profile declares the matching framework, package, or frontend convention.

| Keyword | Skills / Next Hop |
|---------|-------------------|
| aggregate / entity / source-known / source-known-id / entity-type | `drn-domain-design`, `drn-sharedkernel` |
| repository / pagination / dto | `drn-domain-design`, `drn-sharedkernel`, `drn-entityframework` |
| ef-core / migration / prototype-mode | `drn-entityframework`, `drn-testing`, `test-integration-db` |
| testcontainers / postgres / rabbitmq | `drn-testing`, `test-integration-db` |
| authentication / authorization / csp / csrf / nonce | `drn-hosting`, `basic-security-checklist`, `frontend-buildwww-libraries` |
| htmx / bootstrap / rsjs / onmount | `frontend-buildwww-libraries`, `frontend-buildwww-vite` |
| shadow-dom / mount-api / tailwind / react-islands | `frontend-buildwww-react`, `frontend-buildwww-packages` |
| background-job / hosted-service | `drn-hosting`, profile-declared framework package docs |
| mass-transit / messaging / rabbitmq | `drn-testing`, profile-declared framework package docs |

## Related Skills

- [overview-repository-structure.md](../overview-repository-structure/SKILL.md)
- [basic-code-review.md](../basic-code-review/SKILL.md)
