---
name: basic-security-checklist
description: Security development checklist - Input validation, authentication/MFA, CSP/XSS prevention with htmx nonces, CSRF anti-forgery, SQL injection prevention via EF Core, secrets management (IAppSettings), dependency scanning, and security headers. Development-time security guidance aligned with DiSCOS security-first principle. Keywords: security, checklist, input-validation, authentication, authorization, csp, xss, csrf, sql-injection, secrets, dependency-scanning, security-headers, mfa, taint-analysis, skills, basic, drn, hosting
---

# Security Development Checklist

> Development-time security guidance for DRN applications, aligned with DiSCOS "Security First" principle.

## When to Apply
- Adding new endpoints or controllers
- Handling user input (forms, query strings, API payloads)
- Modifying authentication or authorization logic
- Working with external data sources
- Adding new npm or NuGet dependencies
- Configuring deployment or infrastructure

---

## Input Validation

### Server-Side (Always)
```csharp
// Use data annotations on DTOs
public class CreateQuestionRequest
{
    [Required, StringLength(500, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;
    
    [Required, StringLength(10000)]
    public string Body { get; set; } = string.Empty;
}
```

### Rules
- **Never trust client-side validation alone** — always validate server-side
- **Whitelist over blacklist** — define what's allowed, not what's blocked
- **Validate early** — at the entry point (controller/endpoint), before business logic
- **Type-safe parsing** — use `int.TryParse`, `Guid.TryParse` instead of string matching
- **File uploads** — validate MIME type, size limits, scan for malware

### Client-Side (UX Only)
- jQuery Validation Unobtrusive provides immediate feedback
- Never a security boundary — only for user experience

---

## Authentication & Authorization

### MFA
- DRN enforces MFA by default via `DrnProgramBase`
- Never bypass MFA checks in middleware configuration
- Verify `MfaAuthenticated` claim before granting access to sensitive operations

### Rules
- **Least privilege** — grant minimum required permissions
- **Authorize at the boundary** — controller/page level, not inside services
- **Never expose user IDs in URLs** — use Source-Known EntityId (Guid) externally
- **Session management** — use secure cookie policies from `DrnProgramBase`

---

## Secrets Management

### Configuration Layers (IAppSettings)
```
Priority (highest to lowest):
1. Environment variables (production)
2. Mounted settings files (Kubernetes)
3. appsettings.{Environment}.json
4. appsettings.json (defaults only)
```

### Rules
- **Never commit secrets** to source control
- **Never log secrets** — even in debug mode
- **Use `IAppSettings`** for all configuration access
- **Rotate secrets** on suspected compromise
- **Docker secrets** for container deployments

---

## Dependency Security

### When Adding Dependencies
- **Check vulnerability databases** before adding
- **Prefer well-maintained packages** — check last update, download count
- **Pin major versions** — avoid `*` in version ranges
- **Review transitive dependencies** — `dotnet list package --include-transitive`

---

## Security Headers

DRN's `DrnProgramBase` configures these by default:

| Header | Value | Purpose |
|--------|-------|---------|
| `Content-Security-Policy` | Nonce-based strict policy | XSS prevention |
| `X-Content-Type-Options` | `nosniff` | MIME-type sniffing prevention |
| `X-Frame-Options` | `DENY` | Clickjacking prevention |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Referrer leakage prevention |
| `Permissions-Policy` | Restrictive | Feature access control |

### Rules
- **Never weaken CSP** without documented justification
- **Never remove security headers** for debugging in production
- **Test headers** using browser DevTools → Network tab

---

## Quick Pre-Commit Checklist

- [ ] All user input validated server-side
- [ ] No secrets in source code
- [ ] Authorization applied at controller/endpoint level
- [ ] Anti-forgery tokens present on all state-changing operations
- [ ] No `@Html.Raw()` with user-controlled data
- [ ] New dependencies scanned for vulnerabilities
- [ ] Security headers not weakened

---

## Related Skills
- [drn-hosting.md](../drn-hosting/SKILL.md) - Security middleware and CSP configuration
- [frontend-buildwww-libraries.md](../frontend-buildwww-libraries/SKILL.md) - htmx CSP nonce integration
- [basic-code-review.md](../basic-code-review/SKILL.md) - Security review triggers