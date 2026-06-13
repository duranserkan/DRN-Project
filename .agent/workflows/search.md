---
description: Gather structured knowledge context — codebase, knowledge items, skills, web — before running /clarify enrichment
---

> **Standalone or embedded in `/clarify §3`**
> See also: [Operating Model](./_shared/workflow-operating-model.md)
> **Estimated context: ~0.5K tokens**

---

## 1. Role & Mandate
**Context Scout**: Gather high-signal, low-noise inputs under the Context Economy (load minimal, summarize aggressively, stop when sufficient). Budget: max 20% of parent task effort.

Apply the shared Startup Gate before work: read `AGENTS.md`, `.agent/rules/DiSCOS.md` when present, `.agent/repository-profile.md` when present, this workflow, the shared operating model, and only needed skills/resources.

---

## 2. Define Search Intent
Before searching, state intent in one sentence:
> *"I am looking for [topic] to inform [decision/question]."*

---

## 3. Search Sources
Search in priority order; stop when sufficient context is assembled:

### 3.1 Repository Profile + Skills Index
Read `.agent/repository-profile.md` when present, then read `.agent/skills/overview-skill-index/SKILL.md` to map tasks to skills.

### 3.2 Codebase
Locate patterns, configurations, naming, and files:
- **Find paths** by name or glob.
- **Search text** for identifiers, literals, and regular expressions.
- **Inspect outlines/symbols** before reading large source files.
- **Read files** with early exit when the answer is clear.

### 3.3 Knowledge Items
List knowledge resources, then read only relevant resources.

### 3.4 External References (Bounded)
Use only when internal sources are insufficient or the user asks for external/current facts (max 2 web queries). Prefer primary sources and record URL plus retrieval date:
search a specific query, then read only the selected source URLs.

---

## 4. Assemble Output
Return structured context for the caller. `/search` is output-only; when embedded, `/clarify` owns appending this output to the active `CLARIFY-*` document. Use `(by: /search)` source tags:

```markdown
### Relevant Skills
- Relevant: [skill names + why]
- Loaded: [skill names actually read]

### Codebase Findings
- (by: /search) [file:line or pattern] — [key insight, 1 line]

### Knowledge Items
- (by: /search) [KI title] — [key insight, 1 line]

### External References
- (by: /search) [URL, retrieved YYYY-MM-DD] — [key insight, 1 line]

### Initial Observations
- (by: /search) [observation or assumption candidate]

### Not Searched
- [source type] — [reason: budget / not relevant]
```

**Compression Rules**:
- Max **5 bullet points** per source section.
- Each bullet: 1 line, key insight only (no code echoing).
- Include file:line citations for local files when line numbers are available.
- If empty: `[source] — no relevant findings`.

---

## 5. Handoff
- **Embedded**: Return the section to `/clarify`; `/clarify` appends it to `## Enrichment Context` and continues at §4.
- **Standalone**: Present search context and stop.
