---
description: Load portable testing skills and framework-scoped testing skills declared by the profile
---

> **Estimated context: ~5.5K tokens with profile-declared DRN testing skills** (~2.7K portable-only)

Read profile and skills:
   - Read file: `.agent/repository-profile.md` (if present)

*If declared by profile, load framework testing skills first:*
   - Read file: `.agent/skills/drn-testing/SKILL.md`
   - Read file: `.agent/skills/overview-drn-testing/SKILL.md`

*Read portable testing skills:*
   - Read file: `.agent/skills/test-integration/SKILL.md`
   - Read file: `.agent/skills/test-integration-api/SKILL.md`
   - Read file: `.agent/skills/test-integration-db/SKILL.md`
   - Read file: `.agent/skills/test-performance/SKILL.md`
   - Read file: `.agent/skills/test-unit/SKILL.md`
