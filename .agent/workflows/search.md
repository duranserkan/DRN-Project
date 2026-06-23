---
description: Gather structured codebase, knowledge, skill, and web context before /clarify enrichment
---

> **Standalone or embedded in `/clarify §3`**
> See also: [Operating Model](./_shared/workflow-operating-model.md)
> **Estimated context: ~0.6K tokens**

## 1. Mandate

Act as **Context Scout**. Gather high-signal inputs. Load minimally, summarize aggressively, and stop when sufficient. Spend max 20% of parent task effort. Run the shared Startup Gate once; load only needed skills and resources.

## 2. Intent

Before searching, state one sentence:
> *"I am looking for [topic] to inform [decision/question]."*

## 3. Sources

Search in order. Stop when context is sufficient.

1. **Profile and skills**: read `.agent/repository-profile.md` when present; read `.agent/skills/overview-skill-index/SKILL.md` only to map tasks to skills.
2. **Codebase**: find paths, search identifiers/literals/regex, inspect outlines before large files, and stop reading when the answer is clear.
3. **Knowledge items**: list resources, then read only relevant ones.
4. **External references**: use only when internal sources are insufficient or the user asks for external/current facts. Max 2 web queries. Prefer primary sources. Record URL and retrieval date.

## 4. Output

Return structured context only. `/search` does not edit artifacts. When embedded, `/clarify` appends this section to the active `CLARIFY-*` document. Tag bullets with `(by: /search)`.

```markdown
### Relevant Skills
- Relevant: [skill names + why]
- Loaded: [skill names actually read]

### Codebase Findings
- (by: /search) [file:line or pattern] - [key insight, 1 line]

### Knowledge Items
- (by: /search) [KI title] - [key insight, 1 line]

### External References
- (by: /search) [URL, retrieved YYYY-MM-DD] - [key insight, 1 line]

### Initial Observations
- (by: /search) [observation or assumption candidate]

### Not Searched
- [source type] - [reason: budget / not relevant]
```

Compression:
- Max 5 bullets per source section.
- Use one line per bullet; give the key insight only.
- Include file:line citations for local files when available.
- If empty, write `[source] - no relevant findings`.

## 5. Handoff

- Embedded: return the section to `/clarify`; `/clarify` appends it to `## Enrichment Context` and continues at §4.
- Standalone: present search context and stop.
