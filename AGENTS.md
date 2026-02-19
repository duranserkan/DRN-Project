# AGENTS.md — DRN-Project Agent Instructions

> Universal entry point for any AI coding agent working on this repository.

## Behavioral Framework

1. Re-read `DiSCOS/DiSCOS.md` — the Distinguished Secure Cognitive OS.
2. Always sync documents&tests with source of truth(code etc)

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

- **DI**: Attribute-based (`[Scoped]`, `[Singleton]`, `[Transient]`, `[Config]`, `[ConfigRoot]`, `[HostedService]`) — no manual registration
- **Entities**: Source-Known ID pattern (long internal + Guid external)
- **Git**: GitFlow-inspired — `develop` → `master` → tag `v*.*.*`
- **Security**: CSP nonces, CSRF anti-forgery, input validation — see `basic-security-checklist` skill
