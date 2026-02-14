# AGENTS.md — DRN-Project Agent Instructions

> Universal entry point for any AI coding agent working on this repository.

## Behavioral Framework

Read and follow `DiSCOS/DiSCOS.md` — the Distinguished Secure Cognitive OS.
**Security is always the first priority.** Use TRIZ & Priority Stack for all conflicts.

## Project Overview

| Aspect | Detail                                      |
|--------|---------------------------------------------|
| **Type** | .NET 10 framework + DDD reference application |
| **Architecture** | Domain → Infrastructure/Application → Hosted |
| **Frontend** | Razor Pages + htmx + Bootstrap 5 (Vite build) |
| **Testing** | DTT — integration-first with Testcontainers |
| **CI/CD** | GitHub Actions with composite actions       |

## Skill Discovery

- **Skill index**: `.agent/skills/overview-skill-index/SKILL.md` — task→skill & layer→skill lookup
- **Load all skills**: `.agent/workflows/all.md` (cascades: basic → overview → drn → test→ frontend)
- **Individual workflows**: `.agent/workflows/{basic,overview,drn,test,frontend}.md`

## Key Commands

```bash
dotnet build DRN.slnx          # Build solution
dotnet test DRN.slnx           # Run all tests
dotnet run --project Sample.Hosted  # Run sample app
```

## Conventions

- **DI**: Attribute-based (`[Scoped]`, `[Singleton]`, `[Transient], [Config], [ConfigRoot], [HostedService]`) — no manual registration
- **Entities**: Source-Known ID pattern (long internal + Guid external)
- **Git**: GitFlow-inspired — `develop` → `master` → tag `v*.*.*`
- **Security**: CSP nonces, CSRF anti-forgery, input validation — see `basic-security-checklist` skill
