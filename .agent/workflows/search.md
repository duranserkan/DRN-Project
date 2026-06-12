---
description: Gather structured knowledge context — codebase, knowledge items, skills, web — before running /clarify enrichment
---

> **Standalone or embedded in `/clarify §3`**
> **Estimated context: ~0.5K tokens**

---

## 1. Role & Mandate
**Context Scout**: Gather high-signal, low-noise inputs under the Context Economy (load minimal, summarize aggressively, stop when sufficient). Budget: max 20% of parent task effort.

---

## 2. Define Search Intent
Before searching, state intent in one sentence:
> *"I am looking for [topic] to inform [decision/question]."*

---

## 3. Search Sources
Search in priority order; stop when sufficient context is assembled:

### 3.1 Skills Index (Always First)
`view_file .agent/skills/overview-skill-index/SKILL.md` to map tasks to skills.

### 3.2 Codebase
Locate patterns, configurations, naming, and files:
- **Find by name**: `find_by_name` (glob)
- **Find identifiers**: `grep_search` (literal/regex)
- **Structure**: `view_file_outline` → `view_code_item`
- **Read file**: `view_file` (early exit when pattern/answer is clear)

### 3.3 Knowledge Items
```
list_resources  # list KIs
read_resource <uri> # read relevant KIs only
```

### 3.4 External References (Bounded)
Use only when internal sources are insufficient (max 2 web queries):
```
search_web "<specific query>"
read_url_content "<url>"
```

---

## 4. Assemble Output
Write to `## Enrichment Context` in `.agent/temp/CLARIFY-*.md` (embedded) or standalone:

```markdown
### Search Context _(by: /search)_

#### Skills
- Relevant: [skill names + why]
- Loaded: [skill names actually read]

#### Codebase Findings
- [file or pattern] — [key insight, 1 line each]

#### Knowledge Items
- [KI title] — [key insight, 1 line each]

#### External References
- [URL or source] — [key insight, 1 line each]

#### Not Searched
- [source type] — [reason: budget / not relevant]
```

**Compression Rules**:
- Max **5 bullet points** per source section.
- Each bullet: 1 line, key insight only (no code echoing).
- If empty: `[source] — no relevant findings`.

---

## 5. Handoff
- **Embedded**: Append section to `## Enrichment Context` in `.agent/temp/CLARIFY-*.md` and go to §4 (clarify.md).
- **Standalone**: Present search context and stop.
