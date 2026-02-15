---
name: basic-documentation
description: Documentation standards - README structure, ROADMAP patterns, skill documentation format (YAML frontmatter), markdown conventions, security documentation, API docs, and best practices. Guidelines for creating and maintaining all project documentation. Keywords: documentation, readme, roadmap, markdown, documentation-standards, yaml-frontmatter, technical-writing, api-documentation, changelog
last-updated: 2026-02-15
difficulty: basic
---

# Documentation

Standards for creating and maintaining documentation within DRN-Project.

## 1. Core Principles (DiSCOS)

- **Security First**: Document security implications, auth requirements, sensitive data handling.
- **Clarity**: Reader's context first. Define terms on first use.
- **Conciseness**: Front-load key info. Tables for comparisons. Start with TL;DR.
- **Certainty**: "will"/"must" over "maybe"/"might". Quantify uncertainty.
- **Maintenance**: Documentation is code. Update with every significant change.

## 2. README.md Standard

### Structure
1. **Header**: Project Name & Badges (CI/CD, Quality Gate, License, Version)
2. **TL;DR**: 3-5 key value propositions ("You can...")
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

## 3. ROADMAP vs CHANGELOG

### ROADMAP.md

Track planned features with `[ ]`/`[/]`/`[x]` markers, grouped by version.

### CHANGELOG.md

Reverse chronological. Sections: Added, Changed, Fixed, Security, Breaking.

### SemVer

- **Major (X.0.0)**: Breaking changes
- **Minor (0.X.0)**: New features (backward compatible)
- **Patch (0.0.X)**: Bug fixes and security patches

## 4. Agent Skills Documentation

Path: `.agent/skills/XX-skill-name/SKILL.md`

### Frontmatter (YAML)
```yaml
---
name: skill-name
description: Concise description (<100 chars) with keywords. Keywords: keyword1, keyword2
last-updated: YYYY-MM-DD
difficulty: basic | intermediate | advanced
---
```

| Field | Required | Purpose |
|-------|----------|---------|
| `name` | Yes | Skill identifier (matches directory name) |
| `description` | Yes | Concise summary with `Keywords:` suffix for semantic matching |
| `last-updated` | Yes | Date of last meaningful content change — signals staleness |
| `difficulty` | Yes | Triage hint: **basic** (orientation/reference), **intermediate** (domain context needed), **advanced** (deep framework knowledge) |

### Content Sections
1. **Title**: `# Skill Name`
2. **Overview**: Purpose and scope paragraph
3. **Table of Contents**: Required for skills >100 lines
4. **Sections**: Logical breakdown with tables, code blocks, examples
5. **Code Blocks**: Runnable snippets with language specification

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
- **Links**: Relative paths for internal files
- **Code**: Always specify language
- **Diagrams**: Mermaid for architecture/sequence/flow — see [basic-documentation-diagrams](../basic-documentation-diagrams/SKILL.md) for WCAG-compliant styling
- **Tables**: Use for structured data, align columns
- **TOC**: Required for documents >100 lines

## 8. Code Block Best Practices

- Always specify language (```csharp, ```json, ```bash)
- Include necessary imports/usings
- Code should work as-is or clearly indicate placeholders
- Explain non-obvious logic with comments

## 9. Verification Checklist

- [ ] Security implications documented?
- [ ] Scannable in 30 seconds?
- [ ] Code snippets tested and accurate?
- [ ] All public APIs documented?
- [ ] Alt text for images? Semantic structure?
- [ ] Diagrams meet WCAG AA? (see [diagrams skill](../basic-documentation-diagrams/SKILL.md))
- [ ] Relative links valid?
- [ ] TOC present if >100 lines?
- [ ] Concrete examples for complex concepts?
