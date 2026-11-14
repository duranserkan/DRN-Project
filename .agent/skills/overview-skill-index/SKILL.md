---
name: overview-skill-index
description: Choose task skills, map work to skill families, find related skills, and update routing after skills change.
last-updated: 2026-11-13
difficulty: basic
---

# Skill Cross-Reference Index

> Portable skill router. Read `.agent/repository-profile.md` first so profile-declared framework or repository overlays can refine generic routing.

## Selection Rules

1. Load the smallest skill set that covers the task. Reuse the existing owner instead of creating another guidance file.
2. Prefer generic `basic-*`, `overview-*`, and `test-*` skills for portable process guidance.
3. Load framework-specific skills only when the repository profile, source code, or user request names that framework.
4. Load generic `frontend-*` skills only when the repository profile or filesystem declares the matching frontend convention.
5. Load DRN-scoped buildwww skills only when the repository profile or filesystem declares DRN `buildwww`.
6. Load profile-declared custom workflows and skills before generic fallback when they match the task, route, layer, or slash-command flag.
7. Skip profile `Excluded Skills`. Report missing skills and use profile or convention discovery. `/update` reconciles routes.
8. Follow a related-skill link only when its scope is needed. State which public contract or decision it supports. Verify changed claims against current source and report any unverified boundary.

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
| Add DRN buildwww package | repository frontend profile -> `drn-buildwww-packages` when DRN `buildwww` applies |
| Modify DRN Vite/buildwww | `drn-buildwww-vite` when DRN `buildwww` applies |
| Add Razor UI | `frontend-razor-pages-shared` -> `frontend-razor-pages-navigation` -> `frontend-razor-accessors` |
| Add DRN React mounted island | `drn-buildwww-libraries` -> `drn-buildwww-react` when DRN `buildwww` applies |
| Work as an AI agent | `basic-agentic-development` |
| Pursue a goal through workflow routing | `/goal` workflow -> profile/custom route or skill overlays -> `basic-agentic-development` |
| Use repository-specific custom behavior | `.agent/repository-profile.md` -> matching `.agent/workflows/<custom>.md` or `.agent/skills/<custom>-*/SKILL.md` |
| Synchronize two sibling repositories/projects | `/sync` -> `basic-agentic-development` -> `basic-code-review` -> `basic-security-checklist` |
| Refresh local agent instructions/loaders from the current filesystem | `/update` workflow |
| Port `.agent` into a new repository | `/update all` -> full new-repository self-sync -> `/update` verification |

For hosting/API work, combine the profile's hosting skill with `basic-security-checklist` and `test-integration-api`. For persistence, use the profile's ORM skill and `test-integration-db`. Apply `overview-drn-*` only when the profile declares DRN overview/testing conventions.

## Keyword Index

Use broad portable keywords. Apply the profile-scoped terms below only for the matching convention.

| Keyword | Skills |
|---------|--------|
| agentic / ai-agent | `basic-agentic-development` |
| architecture / ddd | `overview-ddd-architecture` |
| ci / github-actions / deployment | `overview-github-actions` |
| documentation / readme / release-notes | `basic-documentation`, `/documentation` |
| frontend / razor-ui | `frontend-razor-pages-shared`, `frontend-razor-pages-navigation`, `frontend-razor-accessors` |
| goal / goal-mode / workflow-routing | `/goal`, profile/custom route overlays, `basic-agentic-development` |
| repository / navigation | `overview-repository-structure` |
| common-parent / sibling-root / cross-repository / cross-project / bidirectional-sync / drift-sync | `/sync`, `basic-agentic-development`, `basic-code-review`, `basic-security-checklist` |
| new repository / port .agent / self-sync | `/update all`, `.agent/repository-profile.md`, `overview-skill-index` |
| repository-specific / custom workflow / custom skill | `.agent/repository-profile.md`, matching `.agent/workflows/<custom>.md`, matching `.agent/skills/<custom>-*/SKILL.md` |
| security | `basic-security-checklist`, `basic-code-review` |
| testing / unit / integration | `test-unit`, `test-integration`, `test-integration-api`, `test-integration-db` |

### Profile-Scoped Keywords

Use these terms only when the repository profile declares the matching framework, package, or frontend convention.

| Keyword | Skills / Next Hop |
|---------|-------------------|
| aggregate / entity / source-known / source-known-id / entity-type | `drn-domain-design`, `drn-sharedkernel` |
| entity-date-filter / created-after / created-before / tick-boundary | `drn-utils`, `drn-testing` |
| repository / pagination / dto | `drn-domain-design`, `drn-sharedkernel`, `drn-entityframework` |
| cancellation / cancellation-scope / repository-cancellation | `drn-utils`, `drn-sharedkernel`, `drn-entityframework` |
| ef-core / migration / prototype-mode | `drn-entityframework`, `drn-testing`, `test-integration-db` |
| testcontainers / postgres / rabbitmq | `drn-testing`, `test-integration-db` |
| frontend / vite / buildwww | `drn-buildwww-vite`, `drn-buildwww-packages` |
| authentication / authorization / csp / csrf / nonce | `drn-hosting`, `basic-security-checklist`, `drn-buildwww-libraries` |
| htmx / bootstrap / rsjs / onmount | `drn-buildwww-libraries`, `drn-buildwww-vite` |
| react / islands | `drn-buildwww-react` |
| shadow-dom / mount-api / tailwind / react-islands | `drn-buildwww-react`, `drn-buildwww-packages` |
| background-job / hosted-service | `drn-hosting`, profile-declared framework package docs |
| mass-transit / messaging / rabbitmq | `drn-testing`, profile-declared framework package docs |

## Related Skills

- [overview-repository-structure.md](../overview-repository-structure/SKILL.md)
- [basic-code-review.md](../basic-code-review/SKILL.md)
