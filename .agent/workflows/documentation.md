---
title: Documentation Workflow
description: Update per-module README.md and RELEASE-NOTES.md from repository-profile modules while preserving human-written content.
---

> **Standalone documentation workflow**
> See also: [Operating Model](./_shared/workflow-operating-model.md)

## 1. Mandate

Act as Documentation Engineer.

- Keep module `README.md` and `RELEASE-NOTES.md` in sync with code.
- Use profile-declared documentation modules. Exclude root docs unless declared.
- Preserve human-written sections unless the user confirms changes.
- Run the shared Startup Gate. Load only needed documentation and source skills.

## 2. Resolve Scope

Use `.agent/repository-profile.md` `Documentation Modules`. If absent, find module folders with manifests plus `README.md` or `RELEASE-NOTES.md`.

| Invocation | Scope | Action |
|---|---|---|
| `/documentation` | Profile/discovered modules | README + release notes |
| `/documentation readme` | Profile/discovered modules | README only |
| `/documentation release-notes` | Profile/discovered modules | Release notes only |
| `/documentation <module>` | Named module/path | README + release notes |
| `/documentation <module> readme` | Named module/path | README only |
| `/documentation <module> release-notes` | Named module/path | Release notes only |

## 3. Load Evidence

For each scoped module:

```text
Read file: <Module>/README.md
Read file: <Module>/RELEASE-NOTES.md
Inspect outline: <Module>/<manifest-file>
```

Read profile source-map files, public API surface, and recent scoped changes.

Skip private/internal-only checks unless needed to prove public behavior or docs accuracy. Record internal-only changes as no docs/release-note trigger.

Use a `.agent/temp/DEVELOP-*.md` artifact only when explicitly supplied or uniquely tied to the module. Block writes if it is `stale: true`, `needs_review: true`, `approval_required: true`, or contains `[ASSUMPTION - unverified]`.

## 4. Scan Drift

Before editing:

1. Extract README headers, referenced types, methods, and config keys.
2. Extract source public elements and observable behavior.
3. Flag divergence:
   - `[STALE]`: docs name missing or renamed code.
   - `[MISSING]`: public behavior lacks docs.
   - `[RENAMED]`: heading or concept drift.
4. Report each finding with Evidence, Impact, Invariant, Recommendation, Confidence, and Verification.
5. Present findings before changes.

## 5. Preserve Content

Do not reorder headings or alter custom narrative without confirmation.

Preserve verbatim:

- Badges block.
- Blockquote description after title.
- Custom sections, voice, dedication, and footers.

## 6. Update README

For new modules or stubs, use:

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
|---|---|
| `<ClassName>` | [purpose] |

## Usage
```text
minimal example
```

## Related
- [link]
````

## 7. Update Release Notes

Apply the [documentation skill's release-note rules](../skills/basic-documentation/SKILL.md#release-notesmd) with the profile's baseline, triggers, and exclusions taking precedence. If no baseline is declared, identify the latest published version before deriving changes. Record `not required` when no trigger applies.

Add or update the current version block above history. Omit empty subsections. Include breaking changes. Keep inferred changes in the plan until source evidence or explicit approval resolves them. Do not publish assumption markers unless requested.

Follow the local template from the profile, package metadata, or framework-owned skill, including required prefix or footer invariants. Otherwise use `## Version X.Y.Z` with `### Breaking Changes`, `### New Features`, `### Changed`, and `### Bug Fixes`.

Preserve historical wording unless editing the current block or fixing malformed package metadata that violates a declared invariant.

## 8. Preview And Confirm

Show the plan and wait for approval before writing:

```markdown
## Documentation Change Plan
### <Module Name>
Detected drift:
- [STALE] Evidence: <file:line or command output> | Impact: <reader/API risk> | Invariant: <rule> | Recommendation: <edit> | Confidence: high/medium/low | Verification: run/not run/blocked/N/A
README.md changes: ...
RELEASE-NOTES.md version: ...
```

## 9. Write And Verify

1. Write approved edits.
2. Read the first 20 lines and touched sections.
3. Run `git diff --check`.
4. Report build/test commands as `not run per repo rule` unless explicitly allowed and relevant.
5. Report evidence, release-note decision, residual risk, and verification.
