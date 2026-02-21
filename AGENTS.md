# AGENTS.md — DRN-Project Agent Instructions

> Universal entry point for any AI coding agent working on this repository.

## Behavioral Framework

1. Re-read `.agent/rules/DiSCOS.md` — the Distinguished Secure Cognitive OS.
2. Always sync `AGENTS.md`, skill files, and tests with the source code

## Project Overview

| Aspect | Detail                                      |
|--------|---------------------------------------------|
| **Type** | .NET 10 framework + DDD reference application |
| **Architecture** | Domain → Infrastructure/Application → Hosted |
| **Frontend** | Razor Pages + htmx + Bootstrap 5 (Vite build) |
| **Testing** | DTT — integration-first with Testcontainers |

## Skill Discovery

- **Skill index**: `.agent/skills/overview-skill-index/SKILL.md` — task→skill & layer→skill lookup
- **Load all skills**: `.agent/workflows/load-skills-all.md` (cascades: basic → overview → drn → test → frontend)
- **Individual workflows**: `.agent/workflows/load-skills-{basic,overview,drn,test,frontend}.md`

## Key Commands

```bash
dotnet build DRN.slnx          # Build solution
dotnet test DRN.slnx           # Run all tests
```

## Conventions

- **DI**: Attribute-based — no manual `services.Add*<>()` for attribute-decorated classes
  - `[Scoped<T>]`, `[Singleton<T>]`, `[Transient<T>]` — service lifetime
  - `[Config("Section")]`, `[ConfigRoot]` — configuration binding
  - `[HostedService]` — background services
- **Entities**: Source-Known ID pattern (long internal + Guid external); `[EntityType(byte)]` required on every entity
- **DTOs**: Derive from `Dto`; live in `*.Contract`; APIs return DTOs only — never entities; expose `Guid` IDs only (Source-Known EntityId: `Guid` externally, never `long Id`)
- **Testing**: DTT (Duran's Testing Technique) — integration-first; `[DataInline]` + `DrnTestContext` for integration, `[DataInlineUnit]` + `DrnTestContextUnit` for unit
- **Frontend**: Razor Pages + htmx + Bootstrap 5; Vite-built assets in `buildwww/`; CSP nonces auto-injected via `NonceTagHelper`; CSRF auto-added on `hx-post/put/delete/patch`
- **Git**: GitFlow-inspired — `develop` → `master` → tag `v*.*.*`; squash merge to develop, merge commit to master
- **Security**: CSP nonces, CSRF anti-forgery, input validation — see `basic-security-checklist` skill

## Workflows

| Slash Command | Purpose |
|---------------|---------|
| `/clarify` | Clarify task → requirements, epics, backlog |
| `/answer` | Answer clarification questions, approve documents |
| `/develop` | Implement from clarified requirements |
| `/review` | Review staged changes or branch diff via Priority Stack |
| `/test` | Add tests for staged changes or a task |
| `/optimize` | Optimize agent-consumed content (skills, workflows, docs) |
| `/update` | Sync AGENTS.md, skill index, workflows from filesystem |
| `/update-last` | Detect changed files from last N commits → delegate to `/update` |
| `/load-skills-basic` | Load: `basic-agentic-development`, `basic-documentation`, `basic-documentation-diagrams`, `basic-security-checklist`, `basic-code-review`, `basic-git-conventions` |
| `/load-skills-overview` | Load: `overview-repository-structure`, `overview-ddd-architecture`, `overview-drn-framework`, `overview-drn-testing`, `overview-github-actions`, `overview-skill-index` |
| `/load-skills-drn` | Load: `drn-sharedkernel`, `drn-entityframework`, `drn-domain-design`, `drn-utils`, `drn-hosting`, `drn-testing` |
| `/load-skills-frontend` | Load: `frontend-buildwww-libraries`, `frontend-buildwww-packages`, `frontend-buildwww-vite`, `frontend-razor-accessors`, `frontend-razor-pages-navigation`, `frontend-razor-pages-shared` |
| `/load-skills-all` | Cascade: basic → overview → drn → test → frontend (~43K tokens) |
