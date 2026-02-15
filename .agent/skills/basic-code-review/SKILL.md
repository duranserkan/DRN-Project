---
name: basic-code-review
description: Code review standards - Priority Stack as review gate (Security→Correctness→Clarity→Simplicity→Performance), DDD boundary validation, attribute-based DI audit, naming conventions, test coverage expectations (DTT), breaking change detection, and security review triggers. Keywords: code-review, review-standards, priority-stack, naming-conventions, breaking-changes, pull-request, quality-gate
last-updated: 2026-02-15
difficulty: intermediate
---

# Code Review Standards

> Structured review criteria for DRN-Project, aligned with DiSCOS Priority Stack.

## When to Apply
- Reviewing pull requests
- Self-reviewing before committing
- Assisting with code review as an agent
- Evaluating architectural changes

---

## Priority Stack Review Gate

Evaluate every change through the Priority Stack, in order:

| Priority | Gate | Key Question |
|:--------:|------|-------------|
| 1 | **Security** | Does this introduce vulnerabilities? |
| 2 | **Correctness** | Does it do what it claims? |
| 3 | **Clarity** | Can someone else understand it in 6 months? |
| 4 | **Simplicity** | Is complexity earned, or accidental? |
| 5 | **Performance** | Is optimization backed by measurement? |

> A change that is fast but incorrect **fails**. A change that is clever but unreadable **fails**. Apply the stack sequentially — higher gates block lower ones.

---

## DDD Boundary Checks

### Review Checklist
- [ ] No `DbContext` references in Domain or Application layers
- [ ] Repository interfaces defined in Domain, implementations in Infrastructure
- [ ] DTOs in Contract, never in Domain

---

## Attribute-Based DI Audit

Every service class should declare its lifetime:

```csharp
// Verify these exist on all service classes
[Scoped]      // Per-request (default for most services)
[Singleton]   // Application lifetime (caches, configurations)
[Transient]   // Per-resolution (lightweight, stateless)
[Config]   // Settings objects mapped from config
[ConfigRoot]   // Settings objects mapped from configroot
[HostedService] // Background services
```

### Review Checklist
- [ ] All service classes have a lifetime attribute
- [ ] `[Singleton]` services hold no mutable per-request state
- [ ] `[Scoped]` services don't capture `[Singleton]` dependencies that hold state
- [ ] No manual `services.AddScoped<>()` calls for classes that should use attributes

---

## Test Coverage Expectations

### DTT Guidance
| Change Type | Expected Tests |
|-------------|---------------|
| New entity/aggregate | Integration test with real DB |
| New API endpoint | API integration test with `CreateClientAsync` |
| Business rule | Unit test for pure logic |
| Repository query | DB integration test with Testcontainers |
| Bug fix | Regression test proving the fix |

### Review Checklist
- [ ] New public API has corresponding test
- [ ] Integration test uses `[DataInline]` + `DrnTestContext`
- [ ] Unit test uses `[DataInlineUnit]` + `DrnTestContextUnit`
- [ ] No test uses `Thread.Sleep` or arbitrary delays
- [ ] Mocks are justified — prefer real containers

---

## Breaking Change Detection

### NuGet Public API
- [ ] No removed public types or members
- [ ] No changed method signatures in public interfaces
- [ ] No changed exception types thrown by public methods
- [ ] Version bump matches semver (breaking = major)

### Database
- [ ] No column removals without migration
- [ ] No type changes without migration
- [ ] Migrations are additive when possible

### Configuration
- [ ] No removed `IAppSettings` properties without fallback
- [ ] New required settings documented in README

---

## Security Review Triggers

Flag for additional security review when changes touch:
- [ ] Authentication or authorization logic
- [ ] New public endpoints
- [ ] Input handling (forms, file uploads, API payloads)
- [ ] CSP configuration or nonce handling
- [ ] Cookie policies or session management
- [ ] External HTTP calls
- [ ] Cryptographic operations
- [ ] User data storage or retrieval

---

## Pre-Mortem: What Could Go Wrong?

Before approving, ask: *"If this change causes an incident in 6 months, what was the root cause?"*

| Failure Mode | What to Look For |
|-------------|-----------------|
| **Silent security regression** | CSP weakened, auth bypassed, new endpoint without `[Authorize]` |
| **Data leak via entity exposure** | API returning entity instead of DTO; `long Id` or `SourceKnownEntityId` in response |
| **Unbounded query** | Missing pagination on collection endpoints; `GetAllAsync()` on large tables |
| **DI lifetime mismatch** | `[Scoped]` service captured by `[Singleton]`; mutable state in singleton |
| **Migration data loss** | Column removal/type change without data migration; non-additive schema change |
| **Broken contract** | Removed public API member; changed method signature; renamed config key without fallback |
| **Test false confidence** | Test passes but doesn't assert meaningful behavior; mock hides the actual bug |
| **Dependency supply chain** | New NuGet/npm package not audited; transitive vulnerability introduced |

> **DiSCOS Mental Model**: Inversion — avoid what must NOT happen, then verify the change doesn't introduce it.

---

## Related Skills
- [basic-security-checklist.md](../basic-security-checklist/SKILL.md) - Security development patterns
- [basic-git-conventions.md](../basic-git-conventions/SKILL.md) - PR and commit standards
- [overview-ddd-architecture.md](../overview-ddd-architecture/SKILL.md) - Layer dependency rules