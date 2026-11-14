---
description: Load all portable skills plus framework-scoped or profile-declared skills
---

// turbo-all

Use for explicit broad context or `/update all`. Otherwise use the smallest task loader.

Read `.agent/repository-profile.md` when present. Apply the skill index's [Selection Rules](../skills/overview-skill-index/SKILL.md#selection-rules), including exclusions and missing-skill reporting. A missing required skill blocks dependent work until safe guidance is resolved. Loading never grants command or VCS authorization.

Read these group loaders in order. Preserve their conditions and skill order. Reuse already-read skills instead of loading duplicates.

| Order | Group and source |
|---|---|
| 1. Basic | [Basic loader](./load-skills-basic.md) |
| 2. Overview | [Overview loader](./load-skills-overview.md) |
| 3. Framework | Profile-declared full load set and its loader. Use the [DRN loader](./load-skills-drn.md) only when the profile or user declares DRN. |
| 4. Testing | [Testing loader](./load-skills-test.md) |
| 5. Frontend | Matching convention subsets in the [frontend loader](./load-skills-frontend.md) |
| 6. Custom | Included profile/custom group loaders alphabetically, then `load-skills-custom.md` when present |

The group loaders and profile own skill membership. `/update` reconciles this ordered composition and verifies its resolved union.
