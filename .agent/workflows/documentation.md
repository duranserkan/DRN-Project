---
description: Generate and update per-module README.md and RELEASE-NOTES.md for DRN.Framework.* packages — maintains documentation in sync with code
---

> **Standalone documentation workflow**
>
> **Estimated context: ~2.9K tokens** (this workflow)

---

## 1. Role & Mandate

**Documentation Engineer** — generate and maintain `README.md` and `RELEASE-NOTES.md` for the seven `DRN.Framework.*` modules. Preserve human-written content. Never overwrite silently.

> [!IMPORTANT]
> **Root `README.md` is excluded** — it is manually maintained and badge/philosophy-heavy.

---

## 2. Resolve Scope & Sub-Command

| Invocation | Scope | Action |
|---|---|---|
| `/documentation` | All 7 modules | Update README + RELEASE-NOTES for each |
| `/documentation readme` | All 7 modules | README only |
| `/documentation release-notes` | All 7 modules | RELEASE-NOTES only |
| `/documentation DRN.Framework.X` | Named module | README + RELEASE-NOTES |
| `/documentation DRN.Framework.X readme` | Named module | README only |
| `/documentation DRN.Framework.X release-notes` | Named module | RELEASE-NOTES only |

**Module list** (canonical):
- `DRN.Framework.SharedKernel`
- `DRN.Framework.Utils`
- `DRN.Framework.EntityFramework`
- `DRN.Framework.Hosting`
- `DRN.Framework.Testing`
- `DRN.Framework.Jobs`
- `DRN.Framework.MassTransit`

---

## 3. Load Context

For each module in scope:

```bash
view_file <Module>/README.md                      # existing content
view_file <Module>/RELEASE-NOTES.md              # existing content
view_file_outline <Module>/<Module>.csproj        # discover source structure
# Then view_file_outline on key source files identified above
```

If a `DEVELOP-*.md` exists with completed PBIs for this module → read it for change content:

```bash
find_by_name "DEVELOP-*.md"  # check root
```

If no DEVELOP doc → fall back to:

```bash
# git log since last version tag for this module
# Synthesize changes — tag inferred items with [ASSUMPTION]
```

### Drift Scan (existing-rich modules)

For existing-rich modules, after loading README and source outline, compare them to surface content drift before proposing any changes:

1. **Extract README topics** — list every `##` and `###` heading. Note types, methods, or config keys mentioned.
2. **Extract source elements** — from `view_file_outline` results, list public types, key methods, and config keys.
3. **Identify divergence** — flag items in either list that are missing or renamed in the other:
   - README references a type/method not found in source → `[STALE]`
   - Source has a public type/key not mentioned in README → `[MISSING]`
   - Section heading no longer matches its source counterpart → `[RENAMED]`
4. **Record findings** — populate the "Detected drift" list in §8 Change Plan.
5. **Skill drift check** — if step 3 found any `[MISSING]` or `[STALE]` items, check the module's corresponding skill:
   - **Resolve skill file**: `drn-<module-suffix>/SKILL.md` (e.g., `DRN.Framework.Utils` → `.agent/skills/drn-utils/SKILL.md`). Skip if no matching skill exists.
   - **Compare skill topics** against the source elements already extracted in step 2.
   - **Flag skill-level drift**: `[SKILL-STALE]` (skill references removed/renamed source element), `[SKILL-MISSING]` (source element not covered in skill).
   - **Record findings** — populate the "Skill drift" list in §8 Change Plan.

> [!NOTE]
> Drift scan is read-only. Findings (including skill drift) are presented to the user in §8 before any write. The user decides which drift items to fix in this run.
>
> Skill drift check is scoped to the module's own skill only — never scan unrelated skills. Only load the skill file when the README drift scan already found divergence.

---

## 4. Identify Human-Written Sections

**Branch on module state:**

- **Existing rich module** (README > 10 lines beyond stub) → §5 existing-first rule applies. The entire file is the preserved baseline. Only specific section *content* may change where code warrants it. Nothing added, removed, or reordered **unless** §3 drift scan surfaces a concrete justification and user approves in §8.
- **New/stub module** (README is `# Module Name` + footer only) → scan for patterns below, then generate from template.

| Pattern | Rule |
|---|---|
| Badge block | All top-of-file lines beginning with `[![` — preserve verbatim as a block, never move or edit |
| Blockquote description | `> …` line(s) immediately after `# Module Name` — preserve verbatim |
| `## TL;DR` section | Preserve verbatim; update only if the listed bullets no longer match source |
| `## Table of Contents` section | Preserve verbatim; update only when sections are added or removed |
| Footer (`---` + `Semper Progressivus`) | Preserve verbatim, always last |
| `Documented with` line | Preserve verbatim |
| Dedication paragraphs | Preserve verbatim, position after version header |
| Custom narrative sections | Preserve verbatim, do not reformat |
| Section order / heading hierarchy | Preserve verbatim — never reorder existing sections |
| All existing sections in synced docs | Preserve structure verbatim; update content only where code changed |

---

## 5. Generate README

> [!IMPORTANT]
> **Existing-first rule**: If the current `README.md` contains content beyond a stub (`# Module Name` + footer only), treat the **entire file as the preserved baseline**. By default, do not reorder sections, remove headings, or replace custom sections — only targeted section *content* changes where a concrete code change (new API, renamed type, removed method) makes the existing content factually wrong or incomplete.
>
> **Structural changes are permitted when complexity justifies them**, but never silently:
> - **Add section** — source gained a significant new capability with no corresponding README heading (`[MISSING]` drift finding)
> - **Remove section** — the section's subject no longer exists in source (`[STALE]` drift finding on the whole section, not just a reference)
> - **Merge sections** — two sections now cover the same functionality and merging reduces confusion without losing information
> - **Split section** — a section grew large enough to obscure navigation and splitting improves clarity
>
> All structural changes must appear in §8 Change Plan with justification and require explicit user confirmation — template below applies only to new/stub modules.

For **new/stub modules**, use this template:

```markdown
# <Module Name>

[Brief description: what the module provides, 2–3 sentences]

## Features

- **<Feature>**: [description]
- **<Feature>**: [description]

## Installation

```bash
dotnet add package <Module Name>
```

## Key Types

| Type | Purpose |
|------|---------|
| `<ClassName>` | [purpose] |

## Usage

```csharp
// minimal usage example
```

## Related

- [Link to related module README]
- [DRN-Project README](../README.md)
````

Fill from: module code (`view_file_outline`), skill file, existing README, DEVELOP-*.md if available.

---

## 6. Generate RELEASE-NOTES

> [!IMPORTANT]
> **Existing-first rule**: If the current `RELEASE-NOTES.md` already contains a version entry, append a **new version block above it** — never rewrite or remove existing version history. Only synthesize content for code changes that occurred after the last documented version.

Use the exact format observed in existing files:

```markdown
Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.

## Version X.Y.Z

[Optional dedication — preserve verbatim if exists]

### New Features

*   **<Category>**
    *   **<Item>**: [description]

### Bug Fixes

*   **<Item>**: [description]

### Breaking Changes

*   **<Item>**: [description and migration path]

### Improvements

*   **<Item>**: [description]

---

Documented with the assistance of [DiSCOS](https://github.com/duranserkan/DRN-Project/blob/develop/DiSCOS/DiSCOS.md)

---
**Semper Progressivus: Always Progressive**
```

Rules:
- Omit empty subsections (`### Bug Fixes`, `### Breaking Changes`, `### Improvements`) — no empty headers
- Footer (`---` + Documented line + `---` + Semper Progressivus) — always preserved verbatim
- Dedications — preserved verbatim, never rewrite
- Mark synthesized (non-DEVELOP-sourced) items with `[ASSUMPTION]`

---

## 7. Quality Gates

Before writing any documentation:

- [ ] Human sections identified and flagged for preservation
- [ ] Version number matches latest tag or DEVELOP-*.md
- [ ] No empty section headers
- [ ] Footer present and verbatim
- [ ] `[ASSUMPTION]` tags on synthesized content

---

## 8. Review & Confirm

Present a **change summary** before writing:

```markdown
## Documentation Change Plan

### <Module Name>

**Detected drift** *(from §3 drift scan — existing-rich modules only)*
- [STALE] `OldType` — not found in source
- [MISSING] `NewFeature` — in source, not documented
- [RENAMED] `OldSection` → `NewSection`

**Skill drift** *(from §3 step 5 — omit if no corresponding skill or no drift found)*
- [SKILL-STALE] `OldUtility` in `drn-<suffix>/SKILL.md` — removed from source
- [SKILL-MISSING] `NewCapability` — in source, not covered by skill
- **Action**: (a) update skill inline during this run, or (b) defer to `/update`

**Structural changes proposed** *(requires explicit confirmation — omit if none)*
- Add `## <NewSection>` — source gained `<NewCapability>`, no heading exists
- Remove `## <OldSection>` — `<OldAPI>` no longer exists in source
- Merge `## A` + `## B` → `## AB` — both cover `<SharedConcept>` since refactor
- Split `## <LargeSection>` → `## X` + `## Y` — section exceeds readable length

**README.md** — [new / updated]
- Content changes: [brief list of additions/modifications]
- Structural changes: [add / remove / merge / split — or "none"]
- Human sections preserved: [list]

**RELEASE-NOTES.md** — [new / updated]
- Version: X.Y.Z
- Sections: New Features (N items), Bug Fixes (N), Breaking Changes (N)
- Human content preserved: [dedication / footer]
- [ASSUMPTION] items: [list if any]
```

Wait for explicit user confirmation before writing. Accept clear affirmatives. A question or qualifier is not confirmation.

> [!NOTE]
> If skill drift is found and user chooses **(a) inline update**, apply the same existing-first rule from §5 to the skill file — targeted content changes only, never restructure. If user chooses **(b) defer**, record the findings and recommend running `/update` scoped to that skill.

---

## 9. Write & Verify

After confirmation:

1. Write each file
2. After writing, `view_file` the first 20 lines to confirm structure
3. Report: `✅ <module>/README.md updated` or `✅ <module>/RELEASE-NOTES.md updated`

If writing fails → report error, stop, ask user for guidance.

---

## Related Skills & Workflows

- `basic-documentation` skill — general documentation principles
- `/review` — run after generation to validate output quality
- `/update` Stage 6 — flags documentation drift and offers delegation here
- `drn-*` skills — module-specific skill files; mapped by convention: `DRN.Framework.<Suffix>` → `.agent/skills/drn-<suffix>/SKILL.md`

> **Symmetry**: `/update` Stage 6 flags README drift → delegates to `/documentation`. This workflow flags skill drift → optionally delegates back to `/update` or handles inline with approval.
