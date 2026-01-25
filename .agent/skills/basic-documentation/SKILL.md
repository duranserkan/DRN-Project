---
name: basic-documentation
description: Documentation standards - README structure, ROADMAP patterns, skill documentation format (YAML frontmatter), markdown conventions, and documentation best practices. Guidelines for creating and maintaining all project documentation. Keywords: documentation, readme, roadmap, markdown, documentation-standards, skill-documentation, yaml-frontmatter, technical-writing
---

# Documentation

This skill defines the standards for creating and maintaining documentation within the DRN-Project ecosystem. It encapsulates patterns for root-level documents (README, ROADMAP) and agent skills.

## 1. Core Principles (DiSCOS)

-   **Clarity**: Reader's context comes first. Define terms on first use.
-   **Conciseness**: Front-load key info. Use tables for comparisons. Start with TL;DR.
-   **Certainty**: Use specific language ("will", "must") over hedges ("maybe", "might").
-   **Maintenance**: Documentation is code. Review and update it with every significant change.

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
6.  **Architecture/Design**: Links to core design documents or external resources.
7.  **Management**: (Optional) Philosophy on Security, Task, Quality management.
8.  **Manifest/Values**: (Optional) Engineering principles or personal manifest.
9.  **Footer**: License, Credits, or inspirational quote.

### Best Practices
-   **Badges**: Use shields.io or SonarCloud badges for live status.
-   **Checklists**: Use `[ ]` and `[x]` to show feature status (e.g., in "About Project" or "Roadmap").

## 3. ROADMAP / Release Notes

Track progress and releases using a `ROADMAP.md` or `RELEASE-NOTES.md` file.

### ROADMAP.md Pattern
Use a checklist format to track features across versions.

```markdown
# Roadmap
- [X] Version 0.6.0 (Released)
- [ ] Version 1.0.0 (Planned)

## Section Name
- [X] Completed Feature
- [ ] Planned Feature
```

### Release Versioning (SemVer)
-   **Major**: Breaking changes.
-   **Minor**: New features (backward compatible).
-   **Patch**: Bug fixes.

## 4. Agent Skills Documentation

All skills in `.agent/skills` must follow this format to be parsable by the agent.

### File Structure
Path: `.agent/skills/XX-skill-name/SKILL.md`

### Frontmatter (YAML)
```yaml
---
description: A concise one-sentence description of what this skill enables or teaches.
---
```

### Content Sections
1.  **Title**: `# XX. Skill Name`
2.  **Overview**: Brief paragraph explaining the purpose.
3.  **Table of Contents**: (Optional) For longer skills, provides quick navigation.
4.  **Sections**: Logical breakdown of the skill (e.g., "1. Architecture", "2. Implementation", "3. Testing").
4.  **Tables**: Use tables for configuration options or decision matrices.
5.  **Code Blocks**: meaningful snippets.

## 5. General Markdown Standards

-   **Headers**: Use ATX style (`#`, `##`).
-   **Lists**: Use hyphens (`-`) for unordered lists.
-   **Links**: Use relative paths for internal files: `[Link](./path/to/file)`.
-   **Code**: Specify language for syntax highlighting (e.g., \`\`\`csharp).
-   **Diagrams**: Use Mermaid for diagrams where helpful.

### Table of Contents
Long documents (typically exceeding 2 screens) should include a Table of Contents for navigability.
-   **Placement**: Locate after the document description/overview.
-   **Format**: Use an unordered list with anchor links to headers.
    ```markdown
    - [Section 1](#section-1)
      - [Subsection](#subsection)
    ```

## 6. Verification

Before committing documentation:
-   [ ] **Scannable**: Can a user get the gist in 30 seconds?
-   [ ] **Accurate**: Do the code snippets actually work?
-   [ ] **Broken Links**: Are all relative links valid?
