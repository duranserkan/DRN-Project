---
description: Load portable overview skills and framework-scoped overview skills declared by the profile
---

> **Estimated context: ~8.2K tokens with profile-declared DRN overview skills** (~4.5K portable-only)

Read profile and skills:
   - Read file: `.agent/repository-profile.md` (if present)
   - Read file: `.agent/skills/overview-repository-structure/SKILL.md`
   - Read file: `.agent/skills/overview-ddd-architecture/SKILL.md`
   - Read file: `.agent/skills/overview-github-actions/SKILL.md`
   - Read file: `.agent/skills/overview-skill-index/SKILL.md`

*If declared by profile, load framework overview skills:*
   - Read file: `.agent/skills/overview-drn-framework/SKILL.md`
   - Read file: `.agent/skills/overview-drn-testing/SKILL.md`
