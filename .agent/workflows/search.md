---
description: Gather structured knowledge context — codebase, knowledge items, skills, web — before running /clarify enrichment
---

> **Standalone or embedded in `/clarify §3`**
>
> **Estimated context: ~1.0K tokens** (this workflow)

---

## 1. Role & Mandate

**Context Scout** — gather the highest-signal, lowest-noise inputs before questions are asked. Apply the Context Economy: load only what is needed, summarize aggressively, stop when sufficient.

> **Budget**: Max 20% of the parent task's total effort. If budget is exhausted before all sources are searched → stop, document what was not searched, proceed.

---

## 2. Define Search Intent

Before searching, state the intent in one sentence:

> *"I am looking for [topic] to inform [decision/question]."*

This guards against scope creep and keeps each search round purposeful.

---

## 3. Search Sources

Search in priority order. Stop as soon as sufficient context is assembled.

### 3.1 Skills Index (always first)

```
view_file .agent/skills/overview-skill-index/SKILL.md
```

Map the task to relevant skills. Note which skills are directly applicable vs. tangential.

### 3.2 Codebase

Use for: existing patterns, naming conventions, configuration keys, file locations.

| Need | Tool |
|------|------|
| Find files by name/pattern | `find_by_name` (glob) |
| Find exact text / identifiers | `grep_search` (literal or regex) |
| Understand a file's structure | `view_file_outline` → `view_code_item` for specifics |
| Read known file | `view_file` |

> **Early exit**: stop reading when the pattern or answer is clear — do not read every file.

### 3.3 Knowledge Items

```
list_resources  # list available knowledge items
# Then: read_resource <uri>  for relevant KIs only
```

Match KI titles/summaries to the search intent. Read only KIs whose summary is directly relevant.

### 3.4 External References (bounded)

Use only when internal sources are insufficient and the gap is concrete (e.g., a library API, a standard, or a URL the user provided).

```
search_web "<specific query>"
read_url_content "<url>"
```

Limit to **2 web queries maximum** per `/search` invocation.

---

## 4. Assemble Output

Write findings into a bounded `## Search Context` section — either directly into `CLARIFY-*.md` under `## Enrichment Context`, or as a standalone output when invoked independently.

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

**Compression rules**:
- Max **5 bullet points** per source section
- Each bullet: one line, key insight only — no file content echoing
- If a source yielded nothing useful → one line: `[source] — no relevant findings`

---

## 5. Handoff

- **When embedded in `/clarify §3`**: append `### Search Context _(by: /search)_` to `## Enrichment Context` in the active `CLARIFY-*.md` and continue to §4 (First-Principles Analysis).
- **When invoked standalone**: present the `## Search Context` block and stop. User or next workflow step consumes the output.
