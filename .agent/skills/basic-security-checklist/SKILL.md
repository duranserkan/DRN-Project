---
name: basic-security-checklist
description: Use when adding or reviewing endpoints, input handling, auth, authorization, CSP, CSRF, secrets, dependencies, security headers, infrastructure, or deployment changes.
last-updated: 2026-06-12
difficulty: intermediate
tokens: ~1K
---

# Security Development Checklist

> Portable development-time security guidance aligned with DiSCOS Security First. Load repository-profile and framework-specific security skills when present.

## When to Apply

- Adding new endpoints, pages, jobs, consumers, or controllers.
- Handling user input: forms, query strings, headers, files, API payloads, or deserialization.
- Modifying authentication, authorization, sessions, cookies, MFA, or tenant isolation.
- Constructing raw SQL, database commands, query expressions, or dynamic filters.
- Working with external data sources or outbound network calls.
- Adding package dependencies, CI steps, containers, or deployment configuration.

## Input Validation

### Server-Side Always

```csharp
public class CreateItemRequest
{
    [Required, StringLength(500, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;
}
```

### Rules

- Never trust client-side validation alone.
- Prefer allowlists over blocklists.
- Validate at the boundary before business logic.
- Use type-safe parsing and framework binders where possible.
- Validate file type, size, storage path, and scanning requirements for uploads.
- Treat deserialization as input handling; constrain types and payload size.

## Authentication And Authorization

- Apply authorization at the boundary: route, controller, page, handler, consumer, or job trigger.
- Enforce least privilege; grant the minimum permission needed.
- Keep internal identifiers, persistence models, and framework-only types out of external contracts.
- Do not bypass MFA, tenant, or scope checks unless the repository profile explicitly defines a safe exception path.
- Use secure cookie/session defaults from the framework or repository profile.

## Secrets Management

- Never commit secrets to source control.
- Never log secrets, tokens, connection strings, or credentials.
- Use orchestrator secret stores or mounted secrets where available.
- If secrets are injected via environment variables, never commit, echo, or log them.
- Use the repository's configuration abstraction and secret store.
- Rotate secrets on suspected compromise.
- Keep sample values fake and clearly marked.

## Raw SQL And Query Construction

- Prefer repository-approved ORMs, query builders, or typed query APIs.
- Use parameterized APIs for raw SQL and database commands.
- Never concatenate or interpolate user-controlled values into SQL, filters, sort expressions, or command text.
- Allowlist dynamic column names, sort directions, and filter fields before translating them into queries.
- Review framework-specific raw query sinks, such as `FromSql`, `ExecuteSql`, command builders, or direct connection APIs.

## Dependency Security

- Check vulnerability and maintenance signals before adding packages.
- Prefer maintained packages with clear provenance.
- Follow the repository pinning policy for NuGet, npm, GitHub Actions, containers, and language-specific package managers.
- Review transitive dependencies when adding security-sensitive packages.
- Do not add dependency upgrades to release notes unless they create a breaking or user-facing behavior change.

## Security Headers And Browser Protections

| Control | Purpose |
|---------|---------|
| CSP | XSS mitigation and script/style control |
| CSRF tokens | State-changing request protection |
| `X-Content-Type-Options` | MIME sniffing mitigation |
| Frame policy | Clickjacking mitigation |
| Referrer policy | Referrer leakage reduction |
| Permissions policy | Browser feature access control |

Rules:

- Never weaken CSP or CSRF protection without documented justification.
- Never remove security headers for production debugging.
- Test headers with browser DevTools or the repository's integration tests when execution is allowed.

## Quick Pre-Commit Checklist

- [ ] All user input is validated server-side.
- [ ] No secrets are in source, logs, docs, or generated artifacts.
- [ ] Authorization is applied at the boundary.
- [ ] State-changing browser requests include CSRF protection.
- [ ] Raw SQL, dynamic filters, and database commands are parameterized or allowlisted.
- [ ] No raw HTML rendering of user-controlled data.
- [ ] New dependencies follow the repository supply-chain policy.
- [ ] Security headers and browser controls are not weakened.

## Related Skills

- [basic-code-review.md](../basic-code-review/SKILL.md) - Security review triggers.
- [frontend-buildwww-libraries.md](../frontend-buildwww-libraries/SKILL.md) - htmx CSP nonce integration when that convention is in use.
