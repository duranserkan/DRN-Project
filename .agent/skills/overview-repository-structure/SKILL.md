---
name: overview-repository-structure
description: Use when navigating an unfamiliar repository, discovering solution organization, locating projects, mapping folders, identifying build/test commands, or updating repository structure docs.
last-updated: 2026-06-12
difficulty: basic
tokens: ~1K
---

# Repository Structure

> Portable repository navigation guide. Read `.agent/repository-profile.md` first when present, then verify with filesystem discovery.

## When to Apply

- Navigating the codebase for the first time.
- Understanding project relationships and dependencies.
- Locating functionality across the repository.
- Updating agent docs after project or folder changes.

## Discovery Order

1. Read `AGENTS.md`.
2. Read `.agent/repository-profile.md` when present.
3. List top-level files and folders.
4. Discover solution, project, package, and workflow files.
5. Confirm assumptions with source files before editing.

## Useful Searches

```bash
rg --files -g '*.sln' -g '*.slnx' -g '*.csproj' -g 'package.json' -g 'vite.config.*' -g 'Dockerfile' -g 'docker-compose*.yml' -g '*.md'
rg --files .github .agent docs
```

## Common Repository Areas

| Area | Convention |
|------|------------|
| Agent guidance | `AGENTS.md`, `.agent/repository-profile.md`, `.agent/skills/`, `.agent/workflows/` |
| Source projects | `src/`, root project folders, or solution-declared folders |
| Tests | `test/`, `tests/`, `*.Test*`, `*.Tests*`, package test scripts |
| Frontend | package root with `package.json`, `vite.config.*`, `src/`, `buildwww/`, or `wwwroot/` |
| CI/CD | `.github/workflows/`, `.github/actions/`, other provider folders |
| Containers | `Dockerfile`, `docker-compose*.yml`, `compose*.yaml`, deployment manifests |
| Docs | `README.md`, `docs/`, module READMEs, release notes, changelog |

## Build And Test Commands

Use the repository profile first. If it is missing:

- Inspect CI workflows for canonical build/test commands.
- Prefer solution-level build commands when a solution file exists.
- Prefer narrow test projects or package scripts for targeted validation.
- Do not run build or test commands unless current instructions allow them.

## Structure Review

- New folders follow existing naming and ownership patterns.
- Shared code lives in shared modules, not copied across feature folders.
- Tests are near the tested layer or in the repository's established test area.
- Generated output is excluded from source unless the repository intentionally tracks it.
- Agent docs reference conventions or profile facts instead of hardcoded paths.

## Related Skills

- [overview-ddd-architecture.md](../overview-ddd-architecture/SKILL.md) - Layering when DDD applies.
- [overview-skill-index.md](../overview-skill-index/SKILL.md) - Skill selection.
