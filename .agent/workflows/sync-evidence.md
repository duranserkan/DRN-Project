---
description: Canonical binary evidence, approval subjects, and baseline storage protocol for sync workflows
---

# Sync Evidence Protocol

> **Owner**: Internal protocol composed by [`/sync`](./sync.md) and [`/sync-execute`](./sync-execute.md).
> See [Shared Contract](./_shared/sync-shared.md) and [Lifecycle](./_shared/status-lifecycle.md).

## Contents

- [1. Evidence Rules](#1-evidence-rules)
- [2. Record Schemas](#2-record-schemas)
- [3. Approval Subjects](#3-approval-subjects)
- [4. Reusable Baseline Store](#4-reusable-baseline-store)

## 1. Evidence Rules

Persist exact binary manifests under the Run ID prefix in `.agent/temp/`. Human-readable views explain evidence but never replace binary artifacts.

Binary format:

```text
ASCII "DRN-SYNC-EVIDENCE" | NUL | 0x01
kind_length:uint64-be | kind:ascii
record_count:uint64-be
record...
```

Record layout:

```text
field_count:uint64-be
(name_length:uint64-be | name:ascii | value_length:uint64-be | value:raw-bytes)...
```

Encoding rules:
- Preserve raw path bytes; never normalize Unicode.
- Integers: ASCII without leading zeros, except `sequence` values which MUST be exactly 20 zero-padded ASCII decimal digits (`00000000000000000000` to `18446744073709551615`). Allocate sequence values monotonically; reject sequence overflow and duplicate sequence values.
- Booleans: `0`/`1`. Hashes: lowercase hex. Missing values: `N/A`.
- Fields emit in schema order; sort records bytewise by key; reject duplicates.
- Hash artifacts with SHA-256 and bind digests into approval subjects.
- Store preimages/patches by hash only when exclusions permit. Never store secrets.

Endpoint IDs: `left`, `right`. Git-root IDs: `shared` (project mode) or `left`/`right` (repository mode).

## 2. Record Schemas

Field order is normative.

| Kind | Sort Key | Ordered Fields |
|---|---|---|
| `control` | `physical-path` | `physical-path`, `device-id`, `mount-id`, `inode`, `file-type`, `mode`, `size`, `sha256` |
| `endpoint-topology` | `endpoint-id` | `run-id`, `common-parent`, `common-device-id`, `common-mount-id`, `common-inode`, `endpoint-id`, `endpoint-name`, `physical-root`, `device-id`, `mount-id`, `inode`, `ancestor-chain-sha256`, `owning-git-root-id` |
| `topology-git` | `git-root-id` | `run-id`, `git-root-id`, `git-top-level`, `topology`, `head`, `ref`, `index-sha256`, `status-sha256`, `refs-sha256` |
| `git-admin-state` | `git-root-id`, `admin-path` | `run-id`, `git-root-id`, `admin-path`, `presence`, `device-id`, `mount-id`, `inode`, `file-type`, `mode`, `size`, `sha256` |
| `scope-input-action` | `root-id`, `path`, `sequence` | `run-id`, `subscope-id`, `sequence`, `root-id`, `path`, `peer-root-id`, `peer-path`, `presence`, `ancestor-chain-sha256`, `device-id`, `mount-id`, `inode`, `file-type`, `mode`, `size`, `sha256`, `git-state`, `dirty-adoption`, `drift-class`, `baseline-relation`, `current-relation`, `source-ownership`, `direction`, `path-policy`, `risk-evidence-sha256`, `decision-rule`, `decision-verdict`, `operation`, `output-presence`, `output-type`, `output-mode`, `output-sha256` |
| `preimage-rollback` | `root-id`, `path`, `sequence` | `run-id`, `subscope-id`, `sequence`, `root-id`, `path`, `preimage-presence`, `preimage-type`, `preimage-mode`, `preimage-sha256`, `preimage-blob-sha256`, `reverse-patch-sha256` |
| `postimage` | `root-id`, `path` | `run-id`, `subscope-id`, `root-id`, `path`, `presence`, `ancestor-chain-sha256`, `device-id`, `mount-id`, `inode`, `file-type`, `mode`, `size`, `sha256`, `verification`, `residual-class` |
| `residual-drift` | `root-id`, `path` | `run-id`, `subscope-id`, `root-id`, `path`, `relation`, `decision`, `variance-id`, `risk` |
| `acceptance-git` | `git-root-id` | `run-id`, `subscope-id`, `git-root-id`, `accepted-base-head`, `accepted-base-ref`, `index-sha256`, `dirty-sha256`, `disjoint-state-sha256` |
| `commit-changed-path` | `git-root-id`, `path` | `run-id`, `subscope-id`, `git-root-id`, `path`, `change-kind`, `post-presence`, `post-type`, `post-mode`, `post-sha256` |
| `commit-tree-entry` | `git-root-id`, `path` | `run-id`, `subscope-id`, `git-root-id`, `path`, `file-type`, `mode`, `sha256` |
| `commit-verification` | `git-root-id` | `run-id`, `subscope-id`, `git-root-id`, `user-commit-sha`, `parent-sha`, `parent-count`, `merge-status`, `changed-paths-sha256`, `accepted-paths-sha256`, `commit-tree-sha256`, `accepted-tree-sha256`, `verified-post-commit-head`, `verified-post-commit-ref`, `index-sha256`, `dirty-sha256`, `disjoint-state-sha256` |
| `preapply-review` | `subscope-id` | `run-id`, `subscope-id`, `review-revision`, `reviewed-preview-sha256`, `scope-input-action-sha256`, `preimage-rollback-sha256`, `rollback-plan-sha256`, `decision-verdict`, `verdict`, `critical-count`, `major-count`, `findings-sha256`, `review-report-sha256` |
| `baseline-selector` | `sequence`, `selector-kind`, `left`, `right` | `selector-kind`, `sequence`, `direction`, `left`, `right` |
| `baseline-store-key` | `selector-id` | `selector-id`, `common-parent`, `common-device-id`, `common-mount-id`, `common-inode`, `left-physical-root`, `left-device-id`, `left-mount-id`, `left-inode`, `right-physical-root`, `right-device-id`, `right-mount-id`, `right-inode`, `topology`, `direction`, `baseline-selector-sha256` |
| `cumulative-checkpoint` | `root-id`, `path` | `run-id`, `subscope-id`, `sequence`, `root-id`, `path`, `presence`, `file-type`, `mode`, `sha256`, `origin`, `origin-commit`, `commit-verification-sha256` |
| `final-verification` | `run-id` | `run-id`, `baseline-store-key-sha256`, `endpoint-topology-sha256`, `topology-git-sha256`, `baseline-selector-sha256`, `direction`, `scope-input-action-sha256`, `cumulative-checkpoint-sha256`, `commit-verification-sha256`, `residual-drift-sha256`, `verdict` |
| `baseline-checkpoint` | `root-id`, `path` | `run-id`, `baseline-store-key-sha256`, `endpoint-topology-sha256`, `topology-git-sha256`, `direction`, `scope-input-action-sha256`, `commit-verification-sha256`, `residual-drift-sha256`, `final-verification-sha256`, `origin`, `origin-commit`, `root-id`, `path`, `presence`, `file-type`, `mode`, `sha256` |

`commit-changed-path` records exact path delta from accepted base to user commit. `commit-tree-entry` records resulting tree. Require actual/accepted tree SHA match before emitting `commit-verification`.

Born branches require `parent-count=1` and `parent-sha=accepted-base-head`. `UNBORN` requires `parent-count=0` and `parent-sha=N/A`. User commit SHA must equal verified post-commit HEAD, differ from accepted base, and match tree digests. Commit verification validates the exact allowed transition: the accepted output becomes exactly the reported commit, the commit has the accepted base as its direct parent, no extra paths enter the commit, pre-existing unrelated staged work remains unchanged in the index, pre-existing unrelated unstaged work remains unchanged in the worktree dirt, accepted output is no longer left staged or dirty, and current HEAD points to the verified commit.

## 3. Approval Subjects

Bind canonical evidence into Apply and Acceptance subjects defined in [Sync Shared Approval Subjects](./_shared/sync-shared.md#4-approval-subjects).

Hash exact subject UTF-8 bytes to compute `approval_subject_sha256`. Set `approval_preview_sha256` to the preview diff SHA-256. Persist records append-only in `.agent/temp/` via the shared Approval Envelope format.

## 4. Reusable Baseline Store

Normalize invocation into `baseline-selector` records with one monotonically increasing sequence allocation across every selector record (including `map-default`):
1. Sequence `00000000000000000000`: Emit `invocation` record with canonical direction (`both`, `left-to-right`, `right-to-left`) and `left=N/A`, `right=N/A`.
2. `only` scope IDs: Canonicalize path/glob selectors to POSIX relative, deduplicate, sort bytewise, and emit `only` records with 20-digit sequence numbers allocated monotonically starting immediately after `invocation` (`00000000000000000001`, `00000000000000000002`...), setting both `left` and `right` to the scope ID and `direction=N/A`.
3. `map` rules: Explicit mappings byte-sorted by `(left, right)` emitting `map` records with 20-digit sequence numbers allocated monotonically continuing directly from the previous allocation (`only` or `invocation`), with `direction=N/A`. When `map` is omitted, emit one `map-default` record with the next monotonically allocated 20-digit sequence number, with `left=same-relative-paths`, `right=same-relative-paths`, and `direction=N/A`. (Eliminates fixed sequence offsets and collisions; zero, one, and more than 100 mappings serialize deterministically with unique sequence values.)
4. Hash binary artifact as `baseline-selector-sha256`.

Generate `baseline-store-key` binding parent/endpoint identities, topology, direction, and `baseline-selector-sha256`.

Store Layout:

```text
.agent/temp/SYNC-BASELINES/<store-key-sha256>/
  versions/<baseline-checkpoint-sha256>.bin
  variances/<residual-drift-sha256>.bin
  current
  promotion.lock
```

`current` format:

```text
format="DRN-SYNC-BASELINE-CURRENT/1"
baseline_sha256="<lowercase-hex>"
variance_sha256="<lowercase-hex>"
```

Select a version only through a valid `current` pointer within an existing key directory. Treat ONLY an absent key directory (`.agent/temp/SYNC-BASELINES/<store-key-sha256>/`) as no baseline (first-comparison semantics). If the key directory exists, fail closed if `current` is absent, `promotion.lock` exists, `current` is malformed, companion version or variance files are missing or digest-mismatched, or baseline checkpoint evidence does not match the normalized invocation.

Promote only after `/sync-execute` proves full final scope. Acquire `promotion.lock` (abort if lock exists), revalidate prior state, write companion version and variance files durably, update `current` atomically, and release `promotion.lock`. Concurrent changes or leftover locks abort promotion and fail closed.
