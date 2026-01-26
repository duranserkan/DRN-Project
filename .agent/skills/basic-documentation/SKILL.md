---
name: basic-documentation
description: Documentation standards - README structure, ROADMAP patterns, skill documentation format (YAML frontmatter), markdown conventions, security documentation, API docs, and best practices. Guidelines for creating and maintaining all project documentation. Keywords: documentation, readme, roadmap, markdown, documentation-standards, skill-documentation, yaml-frontmatter, technical-writing, security-documentation, api-documentation, changelog, skills
---

# Documentation

This skill defines the standards for creating and maintaining documentation within the DRN-Project ecosystem. It encapsulates patterns for root-level documents (README, ROADMAP, CHANGELOG), agent skills, API documentation, and security documentation.

## 1. Core Principles (DiSCOS)

-   **Security First**: Document security implications, authentication requirements, and sensitive data handling.
-   **Clarity**: Reader's context comes first. Define terms on first use.
-   **Conciseness**: Front-load key info. Use tables for comparisons. Start with TL;DR.
-   **Certainty**: Use specific language ("will", "must") over hedges ("maybe", "might").
-   **Maintenance**: Documentation is code. Review and update it with every significant change.
-   **Accessibility**: Use semantic HTML, alt text for images, and clear language.

## 2. README.md Standard

The `README.md` is the entry point for the project. It should be structured to provide immediate value.

### Structure

1.  **Header**: Project Name & Badges (CI/CD, Quality Gate, License, Version).
2.  **TL;DR**: A bulleted list of 3-5 key value propositions. "You can..."
3.  **Navigation**: Quick links to major sections (e.g., `[About Project](#about-project) | [Solution Structure](#solution-structure)`).
4.  **About Project**:
    -   Problem Statement
    -   Solution Overview
    -   Key Characteristics (e.g., Reliability, Security).
5.  **Solution Structure**:
    -   High-level folder breakdown (Src, Docs, Test, Docker).
    -   Component descriptions (Nexus, Framework, Sample).
6.  **Getting Started**: Quick setup instructions with prerequisites.
7.  **Architecture/Design**: Links to core design documents or external resources.
8.  **Security**: Link to security policy, vulnerability reporting, and authentication overview.
9.  **Management**: (Optional) Philosophy on Security, Task, Quality management.
10. **Manifest/Values**: (Optional) Engineering principles or personal manifest.
11. **Footer**: License, Credits, or inspirational quote.

### Best Practices
-   **Checklists**: Use `[ ]` and `[x]` to show feature status.
-   **Examples**: Include minimal working examples where applicable.
-   **Security**: Always mention security considerations upfront.

## 3. ROADMAP vs CHANGELOG

### ROADMAP.md - Future Planning
Track planned features and future versions.

```markdown
# Roadmap

## Version 1.0.0 (Planned Q2 2026)
- [ ] Feature A: User authentication
- [ ] Feature B: API rate limiting

## Version 0.8.0 (In Progress)
- [/] Feature C: Database migration
- [ ] Feature D: Logging improvements
```

### CHANGELOG.md - Historical Record
Document completed changes in reverse chronological order (newest first).

```markdown
# Changelog

## [0.7.0] - 2026-01-26
### Added
- New authentication middleware
- Rate limiting support

### Changed
- Updated database schema

### Fixed
- Memory leak in connection pool

### Security
- Patched SQL injection vulnerability

### Breaking
- Contract change
```

### Release Versioning (SemVer)
-   **Major (X.0.0)**: Breaking changes.
-   **Minor (0.X.0)**: New features (backward compatible).
-   **Patch (0.0.X)**: Bug fixes and security patches.

## 4. Agent Skills Documentation

All skills in `.agent/skills` must follow this format to be parsable by the agent.

### File Structure
Path: `.agent/skills/XX-skill-name/SKILL.md`

### Frontmatter (YAML)
```yaml
---
name: skill-name
description: A concise description (<100 chars) with relevant keywords. Keywords: keyword1, keyword2, skills
---
```

### Content Sections
1.  **Title**: `# Skill Name` (without numbering prefix)
2.  **Overview**: Brief paragraph explaining the purpose and scope.
3.  **Table of Contents**: (Required for skills >100 lines) For quick navigation.
4.  **Sections**: Logical breakdown (e.g., "1. Architecture", "2. Implementation", "3. Testing").
5.  **Tables**: Use for configuration options, decision matrices, or comparisons.
6.  **Code Blocks**: Include meaningful, runnable snippets with language specification.
7.  **Examples**: Concrete examples demonstrating key concepts.

## 5. Security Documentation

Security documentation is **mandatory** for all components handling sensitive data, authentication, or external communication.

### Required Sections
1.  **Threat Model**: Identify assets, threats, and mitigations.
2.  **Authentication & Authorization**: How users/services are authenticated and what they can access.
3.  **Data Protection**: Encryption at rest and in transit, PII handling.
4.  **Vulnerability Reporting**: How to report security issues (e.g., SECURITY.md).
5.  **Dependencies**: Known vulnerabilities in third-party libraries.
6.  **Audit Trail**: Logging and monitoring for security events.

### Example SECURITY.md
```markdown
# Security Policy

## Supported Versions
| Version | Supported          |
| ------- | ------------------ |
| 1.x.x   | :white_check_mark: |
| < 1.0   | :x:                |

## Reporting a Vulnerability
Email security@example.com with:
- Description of the vulnerability
- Steps to reproduce
- Potential impact

We will respond within 48 hours.
```

## 6. API Documentation

Document all public APIs, REST endpoints, and service interfaces.

### REST API Pattern
```markdown
### POST /api/users

Creates a new user account.

**Authentication**: Required (Bearer token)

**Request Body**:
```json
{
  "username": "string",
  "email": "string"
}
```

**Response (201 Created)**:
```json
{
  "id": "uuid",
  "username": "string",
  "createdAt": "ISO8601"
}
```

**Errors**:
- `400`: Invalid request body
- `401`: Unauthorized
- `409`: Username already exists
```

### Code API Pattern
Use XML documentation comments for C# APIs:
```csharp
/// <summary>
/// Validates user credentials and returns an authentication token.
/// </summary>
/// <param name="username">The user's username</param>
/// <param name="password">The user's password</param>
/// <returns>JWT token if valid, null otherwise</returns>
/// <exception cref="ArgumentNullException">If username or password is null</exception>
public string? Authenticate(string username, string password)
```

## 7. General Markdown Standards

-   **Headers**: Use ATX style (`#`, `##`). One `#` per document.
-   **Lists**: Use hyphens (`-`) for unordered lists, `1.` for ordered.
-   **Links**: Use relative paths for internal files: `[Link](./path/to/file)`.
-   **Code**: Always specify language for syntax highlighting (e.g., \`\`\`csharp).
-   **Diagrams**: Use Mermaid for architecture, sequence, or flow diagrams.
-   **Images**: Include alt text: `![Description of image](path/to/image.png)`.
-   **Tables**: Use for structured data, align columns for readability.

### Table of Contents
Long documents (>2 screens or >100 lines) **must** include a Table of Contents.
-   **Placement**: After the document description/overview, before main content.
-   **Format**: Use an unordered list with anchor links to headers.
    ```markdown
    ## Table of Contents
    - [Section 1](#section-1)
      - [Subsection 1.1](#subsection-11)
    - [Section 2](#section-2)
    ```

### Code Block Best Practices
-   **Language**: Always specify (```csharp, ```json, ```bash).
-   **Completeness**: Include necessary imports/usings.
-   **Runnability**: Code should work as-is or clearly indicate placeholders.
-   **Comments**: Explain non-obvious logic.

## 8. Verification Checklist

Before committing documentation:
-   [ ] **Security**: Are security implications documented? Sensitive data handling clear?
-   [ ] **Scannable**: Can a user get the gist in 30 seconds?
-   [ ] **Accurate**: Do code snippets actually work? Are examples tested?
-   [ ] **Complete**: Are all public APIs documented? All configuration options explained?
-   [ ] **Accessible**: Alt text for images? Clear language? Semantic structure?
-   [ ] **Links**: Are all relative links valid? External links working?
-   [ ] **TOC**: Does the document need a Table of Contents (>100 lines)?
-   [ ] **Versioning**: Is the document version or last-updated date clear?
-   [ ] **Examples**: Are there concrete examples for complex concepts?
-   [ ] **Consistency**: Does it follow project conventions and this skill's standards?
