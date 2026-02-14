---
name: basic-code-review
description: Code review standards - Priority Stack as review gate (Security→Correctness→Clarity→Simplicity→Performance), DDD boundary validation, attribute-based DI audit, naming conventions, test coverage expectations (DTT), breaking change detection, and security review triggers. Keywords: code-review, review-standards, priority-stack, ddd-boundaries, naming-conventions, breaking-changes, pull-request, pr-review, quality-gate, skills, basic, security, checklist
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

### Layer Dependency Rules
```
✅ Allowed:
  Presentation → Application → Domain
  Infrastructure → Domain
  Application → Infrastructure (for abstractions such as IEmailSender)
  
❌ Forbidden:
  Domain → Infrastructure (domain must not know persistence)
  Domain → Presentation (domain must not know UI)
  Application → Presentation (application must not know UI)
  Any layer → skipping layers (e.g., Presentation → Domain directly)
```

### Review Checklist
- [ ] No `using Sample.Infra` in Domain project
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

## Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Entity | PascalCase noun | `Question`, `Category` |
| Repository interface | `ISourceKnownRepository<T>` | `ISourceKnownRepository<Question>` |
| Repository impl | `{Entity}Repository` | `QuestionRepository` |
| DbContext | `{Bounded}Context` | `QAContext` |
| Domain event | `{Entity}{Action}Event` | `QuestionCreatedEvent` |
| API endpoint | `/Api/{Area}/[controller]` | `/Api/QA/Question` |
| Page | PascalCase folder/page | `Pages/QA/Questions.cshtml` |

### Source-Known ID Pattern
- **Internal**: `long Id` (auto-increment, for DB joins)
- **External**: `Guid EntityId` (exposed in APIs, URLs)
- **Never expose `long Id`** in public APIs or URLs

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

## Related Skills
- [basic-security-checklist.md](../basic-security-checklist/SKILL.md) - Security development patterns
- [basic-git-conventions.md](../basic-git-conventions/SKILL.md) - PR and commit standards
- [overview-ddd-architecture.md](../overview-ddd-architecture/SKILL.md) - Layer dependency rules
