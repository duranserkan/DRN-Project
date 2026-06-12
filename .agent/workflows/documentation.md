---
description: Generate and update per-module README.md and RELEASE-NOTES.md from repository-profile documentation modules while preserving human-written content.
---

> **Standalone documentation workflow**
> **Estimated context: ~0.6K tokens**

---

## 1. Role & Mandate
**Documentation Engineer**: Maintain module `README.md` and `RELEASE-NOTES.md` files in sync with code. Preserve human-written content and exclude root files unless profile-declared.

---

## 2. Resolve Scope & Sub-Command
Use the default module set from `.agent/repository-profile.md` `Documentation Modules`. Fallback: find folders with project manifests containing `README.md` or `RELEASE-NOTES.md`.

| Invocation | Scope | Action |
|---|---|---|
| `/documentation` | Discovered/profile modules | Update both |
| `/documentation readme` | Discovered/profile modules | README only |
| `/documentation release-notes` | Discovered/profile modules | RELEASE-NOTES only |
| `/documentation <module>` | Named module/path | Update both |
| `/documentation <module> readme` | Named module/path | README only |
| `/documentation <module> release-notes` | Named module/path | RELEASE-NOTES only |

---

## 3. Load Context
For scoped modules:
```bash
view_file <Module>/README.md
view_file <Module>/RELEASE-NOTES.md
view_file_outline <Module>/<manifest-file>
```
Read source files. If `.agent/temp/DEVELOP-*.md` exists, read it; otherwise, infer changes from git history (tag inferred as `[ASSUMPTION]`).

---

## 4. Drift Scan
Scan before editing:
1. Extract README headers, referenced types, methods, and config keys.
2. Extract source public elements.
3. Flag divergence:
   - `[STALE]`: Doc references missing/renamed code element.
   - `[MISSING]`: Source has undocumented public behavior.
   - `[RENAMED]`: Heading mismatch.
4. Present findings before proposing changes.

---

## 5. Preserve Human-Written Sections
Do not reorder headings or modify custom narrative without explicit confirmation. Preserve verbatim:
- Badges block (top of file)
- Blockquote description (after title)
- Custom sections, voice, dedication, and footers

---

## 6. README Updates
For stubs or new modules, use this template:
````markdown
# <Module Name>
[Description]

## Features
- **<Feature>**: [description]

## Installation
```bash
<install command>
```

## Key Types
| Type | Purpose |
|------|---------|
| `<ClassName>` | [purpose] |

## Usage
```text
minimal example
```

## Related
- [link]
````

---

## 7. RELEASE-NOTES Updates
Append new version block *above* existing history. Never rewrite history.
- Include only changes since the last documented version.
- Omit empty subsections; include breaking changes.
- Tag inferred changes with `[ASSUMPTION]`.

---

## 8. Review & Confirm
Present changes and wait for approval before writing:
```markdown
## Documentation Change Plan
### <Module Name>
Detected drift:
- [STALE] ...
README.md structural changes: ...
RELEASE-NOTES.md version: ...
```

---

## 9. Write & Verify
1. Write files.
2. Read first 20 lines and touched sections to verify layout.
3. Run `git diff --check`.
4. Report updates.
