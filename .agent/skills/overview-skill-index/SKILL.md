---
name: overview-skill-index
description: Skill cross-reference index - Task-based skill lookup (add entity, create test, build frontend), layer-based mappings (Domain→Presentation), keyword index, and skill dependency graph. Fast discovery for the right skill combination. Keywords: index, lookup, cross-reference, skill-map, discovery, task-mapping
---

# Skill Cross-Reference Index

> Find the right skill(s) for any task — organized by activity, layer, and keyword.

## When to Apply
- Starting a new development task and unsure which skills to load
- Need to find all related skills for a specific area
- Building context for an unfamiliar part of the codebase

---

## By Task

### "I want to..."

| Task | Skills to Load |
|------|---------------|
| **Add a new entity** | `drn-domain-design` → `drn-sharedkernel` → `drn-entityframework` |
| **Create an API endpoint** | `drn-hosting` → `frontend-razor-accessors` → `basic-security-checklist` |
| **Write integration tests** | `overview-drn-testing` → `test-integration` → `test-integration-api` or `test-integration-db` |
| **Write unit tests** | `overview-drn-testing` → `test-unit` |
| **Add a Razor page** | `frontend-razor-pages-shared` → `frontend-razor-pages-navigation` → `frontend-razor-accessors` |
| **Add JavaScript behavior** | `frontend-buildwww-libraries` → `frontend-buildwww-vite` |
| **Add an npm package** | `frontend-buildwww-packages` → `frontend-buildwww-vite` |
| **Register a service** | `drn-utils` (attribute-based DI) |
| **Add configuration** | `drn-utils` (IAppSettings) |
| **Set up a new project** | `overview-repository-structure` → `overview-ddd-architecture` |
| **Review a PR** | `basic-code-review` → `basic-security-checklist` |
| **Create a release** | `basic-git-conventions` → `github-actions` |
| **Run benchmarks** | `test-performance` |
| **Understand the framework** | `overview-drn-framework` → `overview-repository-structure` |
| **Add background tasks** | `drn-hosting` (HostedService) → `drn-jobs` (planned) |
| **Add messaging** | `drn-masstransit` (planned) |
| **Write documentation** | `basic-documentation` |

---

## By Layer

### Domain Layer
| Concern | Skill |
|---------|-------|
| Entities & Aggregates | `drn-domain-design`, `drn-sharedkernel` |
| Domain Events | `drn-sharedkernel` |
| Repository Contracts | `drn-domain-design` |
| Value Objects | `drn-sharedkernel` |

### Application Layer
| Concern | Skill |
|---------|-------|
| Use Case Orchestration | `overview-ddd-architecture` |
| DTO Mapping | `drn-sharedkernel` |

### Infrastructure Layer
| Concern | Skill |
|---------|-------|
| DbContext & EF Core | `drn-entityframework` |
| Repository Implementation | `drn-domain-design`, `drn-entityframework` |
| Migrations | `drn-entityframework` |

### Presentation Layer
| Concern | Skill |
|---------|-------|
| Razor Pages Layout | `frontend-razor-pages-shared` |
| Navigation | `frontend-razor-pages-navigation` |
| Type-Safe Routing | `frontend-razor-accessors` |
| JavaScript/CSS | `frontend-buildwww-libraries`, `frontend-buildwww-vite` |
| Security Middleware | `drn-hosting` |

### Cross-Cutting
| Concern | Skill |
|---------|-------|
| DI Registration | `drn-utils` |
| Configuration | `drn-utils` |
| Logging | `drn-utils` |
| Testing | `drn-testing`, `overview-drn-testing` |
| Security | `basic-security-checklist`, `drn-hosting` |
| CI/CD | `github-actions` |

---

## Skill Dependency Graph

Skills that should be read together (→ means "read first"):

```
overview-repository-structure → overview-drn-framework → overview-ddd-architecture
drn-sharedkernel → drn-domain-design → drn-entityframework
drn-utils → drn-hosting → drn-testing
frontend-buildwww-vite → frontend-buildwww-libraries
frontend-razor-pages-shared → frontend-razor-pages-navigation → frontend-razor-accessors
overview-drn-testing → test-unit / test-integration → test-integration-api / test-integration-db
basic-security-checklist → basic-code-review
basic-git-conventions → github-actions
```

---

## Keyword Index

| Keyword | Skills |
|---------|--------|
| aggregate | `drn-sharedkernel`, `drn-domain-design` |
| authentication | `drn-hosting`, `basic-security-checklist` |
| bootstrap | `frontend-buildwww-libraries`, `frontend-buildwww-packages` |
| csp / nonce | `drn-hosting`, `frontend-buildwww-libraries`, `basic-security-checklist` |
| ddd | `overview-ddd-architecture`, `drn-domain-design` |
| docker | `github-actions` |
| domain-event | `drn-sharedkernel`, `drn-domain-design` |
| ef-core | `drn-entityframework`, `drn-domain-design` |
| htmx | `frontend-buildwww-libraries`, `frontend-razor-pages-shared` |
| migration | `drn-entityframework` |
| nuget | `github-actions` |
| pagination | `drn-sharedkernel`, `drn-domain-design` |
| rabbitmq | `drn-masstransit` |
| razor | `frontend-razor-pages-shared`, `frontend-razor-pages-navigation`, `frontend-razor-accessors` |
| repository | `drn-domain-design`, `drn-entityframework` |
| rsjs / onmount | `frontend-buildwww-libraries` |
| security | `basic-security-checklist`, `drn-hosting` |
| source-known-id | `drn-domain-design`, `drn-sharedkernel` |
| testcontainers | `drn-testing`, `test-integration-db` |
| vite | `frontend-buildwww-vite` |

---

## Related Skills
- [overview-drn-framework.md](../overview-drn-framework/SKILL.md) - Framework architecture
- [overview-repository-structure.md](../overview-repository-structure/SKILL.md) - Repository layout
