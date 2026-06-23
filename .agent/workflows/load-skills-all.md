---
description: Load all portable skills plus framework-scoped or profile-declared skills
---

// turbo-all

> **Estimated context: ~54.1K tokens** (30 skills plus repository profile)

Read all skills:

**Repository Profile:**
   - Read file: `.agent/repository-profile.md` when present.

**Basic:**
   - Read file: `.agent/skills/basic-agentic-development/SKILL.md`
   - Read file: `.agent/skills/basic-documentation/SKILL.md`
   - Read file: `.agent/skills/basic-documentation-diagrams/SKILL.md`
   - Read file: `.agent/skills/basic-security-checklist/SKILL.md`
   - Read file: `.agent/skills/basic-code-review/SKILL.md`
   - Read file: `.agent/skills/basic-git-conventions/SKILL.md`

**Overview (portable):**
   - Read file: `.agent/skills/overview-repository-structure/SKILL.md`
   - Read file: `.agent/skills/overview-ddd-architecture/SKILL.md`
   - Read file: `.agent/skills/overview-github-actions/SKILL.md`
   - Read file: `.agent/skills/overview-skill-index/SKILL.md`

**Framework-scoped/profile-declared skills:**
   - Read every skill listed in `.agent/repository-profile.md` under `Framework Skill Load Set`.
   - Current DRN framework load set:
     - Read file: `.agent/skills/overview-drn-framework/SKILL.md`
     - Read file: `.agent/skills/overview-drn-testing/SKILL.md`
     - Read file: `.agent/skills/drn-sharedkernel/SKILL.md`
     - Read file: `.agent/skills/drn-entityframework/SKILL.md`
     - Read file: `.agent/skills/drn-domain-design/SKILL.md`
     - Read file: `.agent/skills/drn-utils/SKILL.md`
     - Read file: `.agent/skills/drn-hosting/SKILL.md`
     - Read file: `.agent/skills/drn-testing/SKILL.md`
   - Skip profile-declared skills that do not exist in this repository.

**Testing:**
   - Read file: `.agent/skills/test-integration/SKILL.md`
   - Read file: `.agent/skills/test-integration-api/SKILL.md`
   - Read file: `.agent/skills/test-integration-db/SKILL.md`
   - Read file: `.agent/skills/test-performance/SKILL.md`
   - Read file: `.agent/skills/test-unit/SKILL.md`

**Frontend:**
   - Read file: `.agent/skills/frontend-buildwww-libraries/SKILL.md`
   - Read file: `.agent/skills/frontend-buildwww-packages/SKILL.md`
   - Read file: `.agent/skills/frontend-buildwww-vite/SKILL.md`
   - Read file: `.agent/skills/frontend-razor-accessors/SKILL.md`
   - Read file: `.agent/skills/frontend-razor-pages-navigation/SKILL.md`
   - Read file: `.agent/skills/frontend-razor-pages-shared/SKILL.md`
   - Read file: `.agent/skills/frontend-buildwww-react/SKILL.md`
