# AGENTS.md - Portable Agent Instructions

> Copyable entry point for AI coding agents. Keep this file repository-agnostic; put project facts in `.agent/repository-profile.md`.

## Priority Stack

Security is always the first requirement. Resolve conflicts with TRIZ first, then apply this priority stack:

1. Security: Never Compromise
2. Correctness: Includes Completeness & Accuracy
3. Clarity: Readable > Clever
4. Simplicity: Complexity Must be Earned
5. Performance: Optimize with Proof

## Portability Contract

- Keep `AGENTS.md`, generic skills, and generic workflows reusable across repositories.
- Store repository-specific facts in `.agent/repository-profile.md`: project names, exact commands, module lists, release rules, source maps, and framework selection or overrides.
- Use conventions before hardcoding paths: discover solution files, test projects, frontend package roots, docs folders, and CI workflows from the filesystem.
- Generic skills (`basic-*`, `test-*`, unscoped `frontend-*`) must not require one repository's types, package names, or paths.
- Framework-specific skills must be explicit by name or description, such as `drn-*`, and should only trigger when the repository profile or user request calls for that framework.
- When source code changes a shared fact, update the source-owned docs, relevant repository profile entries, and any framework or repository-owned skills that agents use for that fact.
- When source code or package metadata changes published behavior, update the owning module's release notes according to repository conventions; keep package-specific trigger lists in the repository profile or framework-owned skills.

## Startup

1. Re-read `.agent/rules/DiSCOS.md` when present.
2. Read `.agent/repository-profile.md` when present; treat it as the local overlay for this repository.
3. Load only the skills needed for the current task.
4. Seek supporting and counter examples; do not stop at the examples already shown.
5. Do not build or run tests unless explicitly allowed by the user or the repository profile.

## Discovery Conventions

Use the repository profile first. If it is missing or silent, discover by convention:

| Need | Convention |
|------|------------|
| Build command | Find `*.slnx`, project files, Makefile, package scripts, or CI build jobs. |
| Unit tests | Prefer the profile command; otherwise inspect test projects, package scripts, or CI jobs and run the narrowest allowed command. |
| Integration tests | Run only after unit tests pass and only with explicit permission. |
| Frontend root | Find `package.json`; prefer roots with `vite.config.*`, `buildwww/`, `src/`, or build scripts. |
| Package versions | Treat lockfiles and manifest files as source of truth; do not duplicate pinned versions in skills. |
| Documentation scope | Discover module READMEs, release notes, docs folders, or profile-declared documentation modules. |
| Release notes | Prefer profile/package metadata; update only affected modules for consumer-visible behavior, breaking changes, security fixes, operational defaults, or published package metadata changes other than version-only alignment. |
| Release rules | Prefer profile and CI workflows; otherwise infer from tags, changelog, and package metadata. |

## Skill Discovery

- Skill index: `.agent/skills/overview-skill-index/SKILL.md`.
- Standard overview loader: `.agent/workflows/load-skills-overview.md` is the recommended loader for portable `overview-*` skills.
- Default to existing source-owned guidance with thin loading: read startup/profile context plus only the current workflow route and relevant skills.
- Discover portable overview skills from the skill index and load only the `overview-*` skills relevant to the task.
- Include framework-specific `overview-drn-*` skills only when `.agent/repository-profile.md` declares the repository uses DRN Framework or the active task explicitly needs that framework context.
- Load all skills only when the task explicitly needs broad repository context.
- Prefer task-specific workflows such as `.agent/workflows/load-skills-overview.md`, `.agent/workflows/load-skills-basic.md`, `.agent/workflows/load-skills-test.md`, or repository-profile workflows.

## Working Rules

- Read before editing; use code and docs as source of truth.
- Keep edits scoped to the requested behavior.
- Preserve user changes and unrelated worktree changes.
- Prefer established local patterns over new abstractions.
- Add comments only when they explain non-obvious intent.
- Update documentation and skills when a code or convention change would otherwise create drift.
- Decide whether release notes are required before finishing source, packaging, or published documentation changes; record "not required" when no trigger applies.
- **No CAD Artifact Bypassing**: When workflows such as `/clarify`, `/answer`, or `/develop` are invoked, never skip generating or updating their workspace-local artifacts (e.g. `CLARIFY-*.md`, `DEVELOP-*.md` in `.agent/temp/`). System-level planning artifacts must reference and link to these local pipeline documents rather than bypassing them.
- Run `git diff --check` after documentation or code edits unless blocked.

## Lessons Learned

- File: `AGENTS.LessonsLearned.md` in the repository root, unless the profile overrides it.
- When: reusable mistake, anti-pattern, non-obvious insight, or correction that can change future decisions across cases.
- Exclude: one-time findings, incident history, or case details that cannot be generalized; move durable rules into the owning docs, skills, workflows, or source comments and remove stale lesson entries during cleanup.
- How: append `## N. Title` with dense, scannable subsections that name the concrete case, the general rule, the decision boundary, and the source to update.
- Dedup: read existing entries first and update rather than duplicate.

## Workflows

| Slash Command | Purpose |
|---------------|---------|
| `/goal` | Pursue a goal through the fastest safe workflow route with TRIZ and Priority/Quality Stack gates. |
| `/clarify` | Clarify task into requirements, epics, and backlog. |
| `/answer` | Answer clarification questions and approve documents. |
| `/develop` | Implement from clarified requirements using repository conventions. |
| `/review` | Review staged changes or branch diff via Priority Stack. |
| `/commit-polish` | Commit staged changes and polish non-pushed commit messages. |
| `/test` | Add tests for staged changes or a described task. |
| `/optimize` | Optimize agent-consumed content: skills, workflows, docs. |
| `/search` | Gather structured codebase, docs, skill, and web context before clarification. |
| `/documentation` | Update module documentation and release notes declared by repository conventions. |
| `/update` | Sync agent instructions, skill index, workflows, and profile from filesystem; use after porting `.agent` to a new repository. |
| `/update-last` | Detect changed files from recent commits, then delegate to `/update`. |
