---
title: Documentation Workflow
description: Generate and update per-module README.md and RELEASE-NOTES.md from repository-profile documentation modules while preserving human-written content.
---

> **Standalone documentation workflow**
> See also: [Operating Model](./_shared/workflow-operating-model.md)
> **Estimated context: ~0.6K tokens**

---

## 1. Role & Mandate

**Documentation Engineer**: Maintain module `README.md` and `RELEASE-NOTES.md` files in sync with code. Preserve human-written content and exclude root files unless profile-declared.

Apply the shared Startup Gate before work: read `AGENTS.md`, `.agent/rules/DiSCOS.md` when present, `.agent/repository-profile.md` when present, this workflow, the shared operating model, and only needed documentation/source skills.

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

For scoped modules, use portable tool verbs and map them to the active platform:

```bash
Read file: <Module>/README.md
Read file: <Module>/RELEASE-NOTES.md
Inspect outline: <Module>/<manifest-file>
```

Read source files named by the repository profile source map, public API surface, and recent scoped changes. Use a `.agent/temp/DEVELOP-*.md` artifact only when explicitly supplied or uniquely tied to the requested module; block writes if it is `stale: true`, `needs_review: true`, `approval_required: true`, or contains `[ASSUMPTION - unverified]`.

---

## 4. Drift Scan

Scan before editing and report findings using the shared Evidence Contract:
1. Extract README headers, referenced types, methods, and config keys.
2. Extract source public elements.
3. Flag divergence:
   - `[STALE]`: Doc references missing/renamed code element.
   - `[MISSING]`: Source has undocumented public behavior.
   - `[RENAMED]`: Heading mismatch.
4. For each finding include evidence, impact, invariant, recommendation, confidence, and verification status.
5. Present findings before proposing changes.

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
- Keep inferred changes in the change plan until source evidence or explicit approval resolves them. Do not write assumption markers into published docs unless the user explicitly requests that wording.

---

## 8. Review & Confirm

Present changes and wait for approval before writing:

```markdown
## Documentation Change Plan
### <Module Name>
Detected drift:
- [STALE] Evidence: <file:line or command output> | Impact: <reader/API risk> | Invariant: <rule> | Recommendation: <edit> | Confidence: high/medium/low | Verification: run/not run/blocked/N/A
README.md structural changes: ...
RELEASE-NOTES.md version: ...
```

---

## 9. Write & Verify

1. Write files.
2. Read first 20 lines and touched sections to verify layout.
3. Run `git diff --check`.
4. Report build/test commands as `not run per repo rule` unless explicitly allowed and relevant.
5. Report updates with evidence and residual risk.
