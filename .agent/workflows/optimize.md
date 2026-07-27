---
description: Maximize correctness per token in agent-consumed content while preserving workflow gates
---

> **Trigger**: `/optimize [scope]`
> See [Operating Model](./_shared/workflow-operating-model.md), [Lifecycle](./_shared/status-lifecycle.md), and [`/review`](./review.md).
> **Estimated context: ~0.8K tokens**

## 1. Scope

Run the Startup Gate once and read only targets.

| Scope | Targets |
|---|---|
| Path | Exact path |
| `skills` | `.agent/skills/*/SKILL.md` |
| `workflows` | `.agent/workflows/**/*.md`, including `_shared` |
| `docs` | Root docs plus README/release notes of profile Documentation Modules; otherwise `/documentation` discovery |
| `all` | Skills, workflows, and docs |
| Caller | Preserve every caller-owned gate, including `/review`, CAD, `/goal`, and `/update` mutation, approval, lifecycle, and state-transition gates |
| None | Ask and stop |

Do not optimize `AGENTS.md`, `DiSCOS.md`, or unrelated `.agent/temp/` artifacts unless explicitly scoped. Preserve lifecycle/source metadata and hashes.

## 2. Exact Preview

Preview every edit; `apply` requests preparation, not approval of unseen changes.

| Severity | Gate |
|---|---|
| Safe: whitespace/filler/duplicate phrasing | Exact preview and confirmation |
| Moderate: condensation/restructure | Diff and confirmation |
| Significant: removal/semantic change | Diff, rationale, explicit approval |
| Mixed | Apply only approved items |

For each target:

1. Estimate baseline as `chars / 4`; classify and justify each candidate as optimize/defer/reject.
2. Reject net complexity; tag additions `[COMPLEXITY WARNING]`.
3. Validate references, metadata, gates, and semantic consistency.
4. For semantic workflow/skill changes, run `/review` and resolve Critical findings before preview.
5. Persist `.agent/temp/optimize-apply-preview.md` with the bounded scope, severity, risk decision, proposed patch summary, and a target manifest. List each repository-relative target exactly once in bytewise path order. Record its existence, non-followed file type, mode, raw symlink target or `N/A`, and lowercase SHA-256 of regular-file bytes or raw symlink-target bytes; use `N/A` for missing targets. The target-manifest digest is the lowercase SHA-256 of the manifest block's exact raw UTF-8 bytes, excluding its digest line.
6. Persist the exact patch bytes at `.agent/temp/optimize-proposed.diff`.
7. Compute the raw-byte SHA-256 of both files and present the preview and exact patch for explicit approval.

### Approval Bundle

Use one canonical mapping:

| Approval Field | Value |
|---|---|
| `approval_scope` | Exact mutation and target paths listed in the target manifest |
| `approval_subject` | `.agent/temp/optimize-apply-preview.md` |
| `approval_subject_sha256` | Lowercase SHA-256 of the preview's exact raw bytes |
| `approval_preview_sha256` | Lowercase SHA-256 of `.agent/temp/optimize-proposed.diff` exact raw bytes |

Store the complete shared envelope in `.agent/temp/optimize-approval.md`, never in the preview or patch. The approval file is not part of either digest. Retain superseded records there as append-only history, and accept only the latest record whose envelope, scope, target manifest, preview, and patch all match.

## 3. Optimize

Apply in order:

- Remove filler, hedging, placeholders, restatements, and duplication.
- Condense tables, bullets, definitions, and examples.
- Front-load decisions; use parallel grammar and at most two nesting levels.
- Add content only to fix error, ambiguity, broken reference, or uncovered edge case.
- Simplify indirection and compensating complexity.

Keep security rules, rationale, versions, runnable code, anchors, links, and source keys. Prefer direct conditional steps for workflows, compact tables for skills, inverted-pyramid docs, and executive-summary reports.

## 4. Apply And Verify

After user approval:

1. Recheck every target with non-following filesystem metadata before hashing content. Reject symlink targets or abort on any path-state or type change from the approved manifest (including a previously present target now missing, a previously missing target now added, deletion, rename, mode change, file-type transition such as regular file to symlink, or symlink-target change).
2. Only after all path-state checks pass, recompute every content digest, the target-manifest digest, and the preview and patch raw-byte digests using no-follow reads; abort if any file-type or identity change occurs during reading or on any digest mismatch.
3. Record and verify the complete shared approval envelope in `.agent/temp/optimize-approval.md` using the canonical mapping above.
4. Perform an immediate, final non-following revalidation of path state, file type, and target identity immediately before mutation; abort if any target is a symlink or has changed type or identity.
5. Apply only the approved patch.
6. Verify links, metadata, idempotency, and `git diff --check`.
7. Post-review every Moderate/Significant change; workflow/skill semantics require both pre- and post-review.
8. If a correction changes a target, scope, risk decision, preview, or patch, invalidate the active record, retain it as superseded history, and restart preview.

Report pre/post tokens, delta, severity, and a 0-100 quality score: structure 30%, conciseness 25%, heading density 25%, imperative phrasing 20%. Score zero when any action is ambiguous or omits a documented edge case.

Guarantees: preview-driven, reversible, scoped, Git-tracked, and source-rule preserving.
