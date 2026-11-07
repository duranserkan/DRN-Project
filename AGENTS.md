# AGENTS.md - Portable Agent Instructions

> Portable entry point for AI coding agents. Keep project facts in `.agent/repository-profile.md`.

## Bootstrap and precedence

- `AGENTS.md` and `.agent/rules/DiSCOS.md` are complementary; load either first.
- Re-read agent files only after user changes or when exact current text is needed.

## Priority Stack

Resolve conflicts with TRIZ first, then rank remaining tradeoffs:

1. Security: Never Compromise
2. Correctness: Includes Completeness & Accuracy
3. Clarity: Readable > Clever
4. Simplicity: Complexity Must be Earned
5. Performance: Optimize with Proof

## Portability Contract

- Keep `AGENTS.md`, generic skills, and generic workflows reusable across repositories.
- Put project names, exact commands, modules, release rules, source maps, and framework overrides in `.agent/repository-profile.md`.
- Keep relative documentation links within the portable agent bundle. Resolve repository-local docs and source through the current profile or discovery. Framework skills may link to canonical upstream docs, with version guidance, without requiring a framework source checkout.
- Discover solution files, test projects, frontend roots, docs folders, and CI workflows before hardcoding paths.
- Generic skills (`basic-*`, `test-*`, unscoped `frontend-*`) must not require one repository's types, package names, or paths.
- Trigger framework-specific skills, such as `drn-*`, only when the profile or user request calls for them.
- When code changes a shared fact, update source-owned docs, the profile, and affected framework or repository skills.
- When code or package metadata changes published behavior, update the owning module's release notes by repository rules. Keep package-specific triggers in the profile or framework skills.

## Startup

1. Read `.agent/rules/DiSCOS.md` when present.
2. Read `.agent/repository-profile.md` when present; treat it as the local overlay.
3. Load only task-needed skills.
4. Seek supporting and counter examples; do not stop at the examples already shown.
5. Safe inspection and static checks such as `rg`, `git status`, `git diff --check`, and `wc -m` may run without confirmation. For every `npm` or `dotnet` invocation, including read-only subcommands, require an explicit user request, show the exact command, and wait for confirmation before running it. This also applies when a script or wrapper invokes either tool.
6. Restore, build, run apps, or test only within the allowed execution scope. Profile commands and skill examples do not grant permission to run `npm` or `dotnet`.

## Discovery Conventions

Use the profile first. If it is missing or silent, discover by convention:

| Need | Convention |
|------|------------|
| Build command | Find `*.slnx`, project files, Makefile, package scripts, or CI build jobs. |
| Unit tests | Prefer the profile command; otherwise inspect test projects, package scripts, or CI jobs and run the narrowest allowed command. |
| Integration tests | Run only after unit tests pass and only with explicit permission. |
| Frontend root | Find `package.json`; prefer roots with `vite.config.*`, `buildwww/`, `src/`, or build scripts. |
| Package versions | Treat lockfiles and manifest files as source of truth; do not duplicate pinned versions in skills. |
| Documentation | Discover module READMEs, release notes, docs folders, or profile-declared modules. |
| Release notes | Prefer profile/package metadata; update only affected modules for consumer-visible behavior, breaking changes, security fixes, operational defaults, or published package metadata changes other than version-only alignment. |
| Release rules | Prefer profile and CI workflows; otherwise infer from tags, changelog, and package metadata. |

## Skill Discovery

- Skill index: `.agent/skills/overview-skill-index/SKILL.md`.
- Use `.agent/workflows/load-skills-overview.md` for portable `overview-*` skills.
- Read startup/profile context, the current workflow route, and relevant skills only.
- Use the skill index to select relevant `overview-*` skills.
- Include framework-specific `overview-drn-*` skills only when `.agent/repository-profile.md` declares the repository uses DRN Framework or the active task explicitly needs that framework context.
- Load all skills only when the task explicitly needs broad repository context.
- Prefer task loaders: `load-skills-overview.md`, `load-skills-basic.md`, `load-skills-test.md`, or profile-declared workflows.

## Working Rules

- Every agent file must be under 12,000 characters, including whitespace and newlines. Verify each new or edited agent file with `wc -m`. Replace duplicated guidance with references to its owner. Preserve public behavior, safety gates, and the principles in AGENTS.md and DiSCOS.
- Read before editing. Treat code and source-owned docs as truth.
- **Under Scrutiny**: Agents operate under continuous scrutiny and peer review. Every assumption must be verified against source truth or explicitly marked "[ASSUMPTION - unverified]". Every code modification, recommendation, diff, tool call, and command proposal must be backed by verifiable evidence and transparent rationale.
- Keep edits scoped, minimal, and surgical.
- Use natural language and short sentences. Split long sentences instead of joining them with semicolons.
- Never use em dashes or en dashes.
- Preserve user changes and unrelated worktree changes.
- Prefer established local patterns over new abstractions.
- Name meaningful conditions and operations, especially in UI rendering. Keep simple checks inline.
- Design and optimize for pit of success
- Comment only to explain non-obvious intent.
- Update docs and skills when code or convention changes would otherwise create drift.
- Document only public behavior, not internals.
- Do not stage changes, commit, or amend commits unless the user explicitly authorizes the action. Keep changes unstaged for review by default. Preserve any pre-existing staged changes.
- Decide release-note impact before finishing source, packaging, or published-doc changes; record "not required" when no trigger applies.
- Keep release notes focused on consumer impact. Omit documentation-only edits, internal refactors, and duplicate entries. Follow profile-specific release rules.
- Omit restore/build/run/test/benchmark/load-test steps from plans unless explicitly allowed; use static verification instead.
- **No Unapproved Helper Script Generation or Execution**: Agents must NOT create, write, or execute ad-hoc helper scripts or code (e.g. Python, Bash, Node, custom tools) to automate or perform tasks. Ad-hoc scripts are strictly prohibited under the sync safety contract. If writing or executing an ad-hoc script or code is strictly required for a non-sync task, the agent MUST first provide a detailed description (including purpose, full content, exact commands, and potential risks) and obtain explicit user approval before writing or running it.
- **Digest and Commit Verification on Version Updates**: Whenever container base images, Docker/Docker Compose services, or package dependency versions are added or updated, verify their content digests (or provenance hashes) against canonical registry manifests or repository metadata prior to committing. Require third-party GitHub Actions to use full immutable Git commit SHAs (with version comments), verifying tag-to-commit alignment separately according to the repository profile.
- **No CAD Artifact Bypassing**: `/clarify`, `/answer`, and `/develop` must create or update workspace-local artifacts such as `CLARIFY-*.md` and `DEVELOP-*.md` in `.agent/temp/`. System plans must reference and link those documents.
- After `/clarify`, route target edits through `/answer`, a valid `DEVELOP-*` handoff, and `/develop`. Manual mode waits for the next workflow invocation. Automatic progression must satisfy the owning autonomy and approval gates.
- Run `git diff --check` after documentation or code edits unless blocked.

## Lessons Learned

- File: `AGENTS.LessonsLearned.md` in the repository root, unless the profile overrides it.
- When: reusable mistake, anti-pattern, non-obvious insight, or correction that can change future decisions across cases.
- Exclude: one-time findings, incident history, and non-general case details.
- Move durable rules into owning docs, skills, workflows, or source comments; remove stale lessons during cleanup.
- Format: append `## N. Title` with dense subsections for case, rule, boundary, and source to update.
- Dedup: read existing entries before adding one.

## Workflows

If the active agent platform does not support slash commands, execute the named workflow steps manually. Treat `/clarify X` as "follow `.agent/workflows/clarify.md` with input X"; apply the same mapping to other routes.

| Slash Command | Purpose |
|---------------|---------|
| `/goal` | Pursue a goal through the fastest safe workflow route with TRIZ and Priority/Quality Stack gates. |
| `/clarify` | Clarify task into requirements, epics, and backlog. |
| `/answer` | Answer clarification questions and approve documents. |
| `/develop` | Implement from clarified requirements using repository conventions. |
| `/sync` | Detect and reconcile scoped drift between exactly two sibling repositories or projects from their physical common parent. |
| `/review` | Review staged changes or branch diff via Priority Stack. |
| `/commit-polish` | Commit staged changes and polish non-pushed commit messages. |
| `/test` | Add tests for staged changes or a described task. |
| `/optimize` | Optimize agent-consumed content: skills, workflows, docs. |
| `/search` | Gather structured codebase, docs, skill, and web context before clarification. |
| `/documentation` | Update module documentation and release notes declared by repository conventions. |
| `/update` | Sync agent instructions, skill index, workflows, and profile from filesystem; use after porting `.agent` to a new repository. |
| `/update-plan` | Discover skills, projects, assets, and drift, then generate `.agent/temp/update-plan.md`. |
| `/update-execute` | Execute reviewed update-plan sync stages and record completion state. |
| `/update-verify` | Verify update structural integrity and mark the update plan verified or failed. |
| `/update-last` | Detect changed files from recent commits, then delegate to `/update`. |
