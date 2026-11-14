---
name: basic-documentation
description: Use when writing or reviewing public documentation, READMEs, release notes, API guides, or agent skills.
last-updated: 2026-11-14
difficulty: basic
---

# Documentation

Standards for creating and maintaining repository documentation.

## 1. Core Principles (DiSCOS)

- **Security First**: Document security implications, auth requirements, sensitive data handling.
- **Clarity**: Reader's context first. Define terms on first use.
- **Conciseness**: Lead with the outcome. Use short sentences and tables for comparisons. Avoid prose semicolons and em/en dashes.
- **Evidence**: State verified facts directly. Identify uncertainty rather than converting it into a promise.
- **Public behavior**: Document supported usage, contracts, configuration, and migration. Inspect internals only to verify those claims.
- **Maintenance**: Update the owner when public behavior, usage requirements, or durable conventions change. Routine repairs to an unchanged contract belong in code and regression coverage, not repeated descriptions in agent files. Link to the owner instead of duplicating guidance. Assess release-note impact separately.
- **Shared Defaults**: Cross-package defaults and docs-sync rules belong in the repository profile or the owning source documentation. Package READMEs must stay self-contained for important facts and may reference sibling package READMEs for related guidance.

## 2. README.md Standard

### Structure
1. **Header**: Project Name & Badges (CI/CD, Quality Gate, License, Version)
2. **Summary**: 3-5 key benefits ("You can...")
3. **Navigation**: Quick links to major sections
4. **About Project**: Problem Statement, Solution Overview, Key Characteristics
5. **Solution Structure**: Folder breakdown, component descriptions
6. **Getting Started**: Quick setup with prerequisites
7. **Architecture/Design**: Links to design documents
8. **Security**: Link to security policy, vulnerability reporting
9. **Management**: (Optional) Philosophy on Security, Task, Quality
10. **Footer**: License, Credits

### Best Practices
- Use `[ ]`/`[x]` checklists for feature status
- Include minimal working examples
- Mention security considerations upfront

## 3. ROADMAP vs CHANGELOG vs RELEASE-NOTES

### ROADMAP.md

Track planned features with `[ ]`/`[/]`/`[x]` markers, grouped by version.

### CHANGELOG.md

Repository/service-level history. Reverse chronological. Sections: Added, Changed, Fixed, Security, Breaking.

### RELEASE-NOTES.md

Per-package/module release notes. Use the repository profile or package metadata for the exact template, heading style, sections, prefix, and footer. Maintain versioned changes above existing history.

Update release notes when a change affects a published module's consumers or package metadata other than version-only alignment:
- Public API, contract, CLI, endpoint, event, DTO, configuration key, default, security posture, operational behavior, data/migration behavior, or observable bug fix.
- Dependency, runtime, container, or build-output changes only when they are breaking, security-relevant, consumer-visible, or alter published package artifacts.
- Published docs that are shipped as package metadata, such as package READMEs or release notes.

Do not add release-note entries for internal-only refactors, tests, comments, private/internal-only checks, agent-only docs, or routine dependency-only updates unless the repository profile declares a stricter rule.

For version-aligned releases, leave unchanged packages alone unless the profile requires entries. Record release notes as "not required" when no trigger applies.

### SemVer

- **Major (X.0.0)**: Breaking changes
- **Minor (0.X.0)**: New features (backward compatible)
- **Patch (0.0.X)**: Bug fixes and security patches

## 4. Agent Skills Documentation

Path: `.agent/skills/<skill-name>/SKILL.md`. Keep every agent file below 12,000 characters. Verify edited files with `wc -m`.

### Frontmatter (YAML)
```yaml
---
name: skill-name
description: Use when the task needs this skill's specific guidance.
last-updated: YYYY-MM-DD
difficulty: basic | intermediate | advanced
---
```

| Field | Required | Purpose |
|-------|----------|---------|
| `name` | Yes | Skill identifier (matches directory name) |
| `description` | Yes | Concise task trigger that distinguishes this skill from adjacent skills |
| `last-updated` | Yes | Date of last meaningful content change |
| `difficulty` | Yes | Triage hint: **basic** (orientation/reference), **intermediate** (domain context needed), **advanced** (deep framework knowledge) |

### Content Sections
1. **Title**: `# Skill Name`
2. **Overview**: Purpose and scope paragraph
3. **Navigation**: Add a table of contents when section discovery needs it. Avoid copying an existing index.
4. **Sections**: Logical breakdown with tables, code blocks, examples
5. **Code Blocks**: Follow Section 8. Prefer one verified example over repeated boilerplate.

Keep one owner for each rule and link to it. Split only when separate loading helps a distinct task. Preserve entry points, incoming anchors, safety gates, and public edge cases when consolidating.

## 5. Security Documentation

Mandatory for components handling sensitive data, auth, or external communication.

**Required Sections**: Threat Model, Auth & Authorization, Data Protection, Vulnerability Reporting, Dependencies, Audit Trail.

## 6. API Documentation

### REST API Pattern
```markdown
### POST /api/users
Creates a new user account.
**Authentication**: Required (Bearer token)
**Request/Response**: JSON with status codes (201, 400, 401, 409)
```

### Code API Pattern
Use XML documentation comments (`<summary>`, `<param>`, `<returns>`, `<exception>`).

## 7. Markdown Standards

- **Headers**: ATX style (`#`), one `#` per document
- **Lists**: Hyphens (`-`) unordered, `1.` ordered
- **Links**: Relative paths for files shipped together. Portable agent files must not link outside their bundle to repository-local docs or source. Use current-profile discovery for local files and canonical upstream URLs for external framework references. Check the installed version before applying upstream guidance.
- **Code**: Always specify language
- **Diagrams**: Use [basic-documentation-diagrams](../basic-documentation-diagrams/SKILL.md) for Mermaid accessibility checks
- **Tables**: Use for structured data, align columns
- **Navigation**: Add section links when they improve discovery

## 8. Code Block Best Practices

- Always specify language (```csharp, ```json, ```bash)
- Include necessary imports/usings
- Verify API names and supported usage. Label placeholders explicitly
- Do not claim execution unless it was authorized and performed
- Explain non-obvious logic with comments

## 9. Verification Checklist

- [ ] Security implications documented?
- [ ] Scannable in 30 seconds?
- [ ] Public claims and examples verified against current source?
- [ ] All public APIs documented?
- [ ] Alt text for images? Semantic structure?
- [ ] Diagrams meet WCAG AA? (see [diagrams skill](../basic-documentation-diagrams/SKILL.md))
- [ ] Relative links valid, and portable agent references usable without this repository's source/docs?
- [ ] Navigation helps readers find the relevant section?
- [ ] Concrete examples for complex concepts?
