---
name: basic-code-review
description: Use when reviewing code changes, pull requests, staged diffs, or self-reviewing work for security, correctness, clarity, simplicity, performance, breaking changes, and missing verification.
last-updated: 2026-11-13
difficulty: intermediate
---

# Code Review Standards

> Portable review criteria. Load `.agent/repository-profile.md` first when present, then apply any local framework rules as an overlay.

## Scrutiny Mandate

Review diffs, commits, PRs, and agent output critically. Support claims with repository evidence, static checks, or authorized execution. Do not claim verification without evidence or bypass execution gates.

## Repository Context Gate

1. Read `AGENTS.md` and `.agent/repository-profile.md` when present.
2. Load only relevant scoped skills, such as framework, frontend, testing, or security skills.
3. Verify source-of-truth files instead of assuming conventions from examples.
4. Treat generic checklist items as prompts, not proof that a repository uses a specific framework.

## Priority Stack Review Gate

Resolve contradictions with TRIZ first. Then evaluate every change in order:

| Priority | Gate | Key Question |
|:--------:|------|--------------|
| 1 | Security | Does this introduce vulnerabilities or weaken safeguards? |
| 2 | Correctness | Does it do what it claims under realistic conditions? |
| 3 | Clarity | Can someone else understand it in 6 months? |
| 4 | Simplicity | Is complexity earned, or accidental? |
| 5 | Performance | Is optimization justified by measurements or clear source evidence? |

## Architecture Checks

- No dependency points from stable/core layers to volatile infrastructure layers unless the repository architecture explicitly allows it.
- External contracts do not leak persistence internals, framework-only types, or private identifiers.
- DTOs, API models, and persistence entities stay separated when the repository uses that boundary.
- New abstractions remove real complexity or match an established local pattern.
- Apply the [refactoring guidance](../basic-agentic-development/SKILL.md#refactoring-for-clarity): names should expose intent, and extraction must preserve behavior. Static inspection supports equivalence but does not establish runtime or visual correctness.
- Check unnecessary allocations, repeated work, and whether algorithms and data structures fit usage. Require source evidence or measurements for claimed benefits, explicit assumptions, and preserved correctness.
- Public behavior changes are documented and classified as breaking or non-breaking.
- Follow the repository profile's runtime-access conventions.
- Apply the [deprecated-code rule](../basic-agentic-development/SKILL.md#anti-patterns).
- Prefer pit of success.
- Verify third-party API accessibility independently of XML documentation, which may include internal members. Prefer documented public factories and registration APIs over constructing implementation types.

## Diagnostic Suppressions

Prefer suppression attributes when supported. In C#, apply `[SuppressMessage]` to the narrowest applicable member with the exact rule ID and a concrete `Justification`. Use the containing method for local-variable diagnostics. Avoid test-class suppressions when a property, fixture type, or method can own them. Use `#pragma warning disable` only when attributes cannot suppress the diagnostic at the required location. Explain why and restore it immediately afterward. Preserve instance getters used by reflection and interface dispatch coverage.

## UI Accessibility

- Prefer native buttons for actions and anchors with `href` for navigation. Set `type="button"` on non-submit buttons.
- Prefer native elements when their semantics fit. Preserve accessible names, live announcements, layout, and visible focus when replacing markup. Align affected test fixtures.
- If a native control cannot be used, provide the appropriate role, keyboard focus, keyboard activation, and visible focus indicators.

## Dependency Injection And Configuration

- Check registrations against the repository's declared convention, whether attributes or manual wiring.
- Singleton services do not capture mutable request/user state.
- Scoped dependencies are not retained by longer-lived services.
- New required configuration has defaults, validation, documentation, or clear deployment instructions.
- Follow the repository profile's environment conventions. Use explicit test configuration without weakening production invariants.
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

### Documentation And Release Notes
Apply the profile's release rules and [documentation triggers](../basic-documentation/SKILL.md#release-notesmd). Document consumer impact only. Record "not required" when no trigger applies.

## Security Review Triggers

Flag for extra scrutiny when changes touch:

- Authentication, authorization, tenant isolation, or identity.
- New public endpoints or external network calls.
- Input handling: forms, files, deserialization, query strings, headers, or API payloads.
- CSP, CSRF, CORS, cookies, sessions, redirects, or rate limits.
- Cryptography, token handling, secrets, or certificate validation.
- User data storage, export, logging, or telemetry.
- CI/CD, package restore, dependency provenance, Dockerfiles, or deploy scripts.
- Runtime versions, environment defaults, or deployment configuration. Check alignment with the profile and project metadata.

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
| Supply-chain risk | Dependency, action, or image that fails the [dependency security checks](../basic-security-checklist/SKILL.md#dependency-security), including canonical digest/provenance verification and full immutable action SHAs |
| Runtime drift | Container image/runtime version no longer matches repository profile or project metadata |

## Related Skills

- [basic-security-checklist.md](../basic-security-checklist/SKILL.md) - Security development patterns.
- [basic-git-conventions.md](../basic-git-conventions/SKILL.md) - PR and commit standards.
- [overview-ddd-architecture.md](../overview-ddd-architecture/SKILL.md) - Use only when the repository follows DDD layering.
