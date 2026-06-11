---
name: basic-code-review
description: Use when reviewing code changes, pull requests, staged diffs, or self-reviewing work for security, correctness, clarity, simplicity, performance, breaking changes, and missing verification.
last-updated: 2026-06-12
difficulty: intermediate
tokens: ~1.3K
---

# Code Review Standards

> Portable review criteria. Load `.agent/repository-profile.md` first when present, then apply any local framework rules as an overlay.

## Repository Context Gate

Before reviewing, identify the local conventions that are actually in force:

1. Read `AGENTS.md` and `.agent/repository-profile.md` when present.
2. Load only relevant scoped skills, such as framework, frontend, testing, or security skills.
3. Verify source-of-truth files instead of assuming conventions from examples.
4. Treat generic checklist items as prompts, not proof that a repository uses a specific framework.

## Priority Stack Review Gate

Evaluate every change in order:

| Priority | Gate | Key Question |
|:--------:|------|--------------|
| 1 | Security | Does this introduce vulnerabilities or weaken safeguards? |
| 2 | Correctness | Does it do what it claims under realistic conditions? |
| 3 | Clarity | Can someone else understand it in 6 months? |
| 4 | Simplicity | Is complexity earned, or accidental? |
| 5 | Performance | Is optimization backed by measurement? |

A change that is fast but incorrect fails. A change that is clever but unreadable fails.

## Architecture Checks

- No dependency points from stable/core layers to volatile infrastructure layers unless the repository architecture explicitly allows it.
- External contracts do not leak persistence internals, framework-only types, or private identifiers.
- DTOs, API models, and persistence entities stay separated when the repository uses that boundary.
- New abstractions remove real complexity or match an established local pattern.
- Public behavior changes are documented and classified as breaking or non-breaking.

## Dependency Injection And Configuration

- Enforce the repository's declared registration convention only. If the profile says attributes are used, check attributes. If it says manual registration is used, check registration code.
- Singleton services do not capture mutable request/user state.
- Scoped dependencies are not retained by longer-lived services.
- New required configuration has defaults, validation, documentation, or clear deployment instructions.
- Secrets are not hardcoded, logged, committed, or documented as real values.

## Test Coverage Expectations

| Change Type | Expected Signal |
|-------------|-----------------|
| Security or authorization change | Focused regression test or explicit security review evidence |
| Public API behavior | Contract/API test or documented compatibility reasoning |
| Business rule | Unit or component test for the rule |
| Persistence/query behavior | Integration or database-backed test where mocks would hide risk |
| Bug fix | Regression test proving the old failure mode is covered |
| Documentation-only change | Link/path validation, drift scan, or targeted review |

Respect repository rules about running builds and tests. If execution is not allowed, report verification as not run instead of implying pass/fail.

## Breaking Change Detection

### Public API
- No removed public types or members without a breaking-change note.
- No changed method signatures in public interfaces without migration guidance.
- Optional parameters do not duplicate overload shapes or create ambiguous call sites.
- Exception behavior changes are intentional and documented.
- Versioning matches the repository's release policy.

### Database And Data
- No column removals or type changes without migration and data-safety reasoning.
- Destructive migrations have rollback or recovery guidance.
- New required data has safe defaults or deployment sequencing.

### Configuration And Operations
- Removed or renamed settings have compatibility handling or a breaking-change note.
- Defaults changing runtime behavior are documented.
- Operational docs and release notes mention user-facing behavior changes.

## Security Review Triggers

Flag for extra scrutiny when changes touch:

- Authentication, authorization, tenant isolation, or identity.
- New public endpoints or external network calls.
- Input handling: forms, files, deserialization, query strings, headers, or API payloads.
- CSP, CSRF, CORS, cookies, sessions, redirects, or rate limits.
- Cryptography, token handling, secrets, or certificate validation.
- User data storage, export, logging, or telemetry.
- CI/CD, package restore, dependency provenance, Dockerfiles, or deploy scripts.
- Container base images, runtime versions, environment defaults, or deployment config alignment with repository profile and project metadata.

## Pre-Mortem

Before approving, ask: "If this causes an incident in 6 months, what was the root cause?"

| Failure Mode | What to Look For |
|-------------|------------------|
| Silent security regression | Guard removed, endpoint exposed, policy weakened |
| Data leak | Internal fields, private identifiers, secrets, logs, or entities exposed |
| Unbounded work | Missing pagination, unbounded query, retry storm, large in-memory load |
| Lifetime mismatch | Captive dependency, mutable singleton, leaked disposable |
| Data loss | Destructive migration, overwrite path, missing backup/rollback |
| Broken contract | Removed member, changed response shape, renamed config key |
| False confidence | Test asserts plumbing only, mock hides integration failure |
| Supply-chain risk | New package/action/image without provenance or pinning policy |
| Runtime drift | Container image/runtime version no longer matches repository profile or project metadata |

## Related Skills

- [basic-security-checklist.md](../basic-security-checklist/SKILL.md) - Security development patterns.
- [basic-git-conventions.md](../basic-git-conventions/SKILL.md) - PR and commit standards.
- [overview-ddd-architecture.md](../overview-ddd-architecture/SKILL.md) - Use only when the repository follows DDD layering.
