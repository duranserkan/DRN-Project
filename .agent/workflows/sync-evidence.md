---
description: Canonical binary evidence, approval subjects, and baseline storage protocol for sync workflows
---

# Sync Evidence Protocol

> **Owner**: Internal protocol composed by [`/sync`](./sync.md) and [`/sync-execute`](./sync-execute.md).
> **Purpose**: keep byte-exact path and state evidence machine-stable while operational workflows remain readable.

## Contents

- [1. Evidence Rules](#1-evidence-rules)
- [2. Record Schemas](#2-record-schemas)
- [3. Approval Subjects](#3-approval-subjects)
- [4. Reusable Baseline Store](#4-reusable-baseline-store)

## 1. Evidence Rules

Persist exact manifests as binary files beneath the validated Run ID prefix in the controlling `.agent/temp/`. Human-readable views explain evidence but never replace binary companions.

Binary format:

```text
ASCII "DRN-SYNC-EVIDENCE" | NUL | 0x01
kind_length:uint64-be | kind:ascii
record_count:uint64-be
record...
```

Each record is:

```text
field_count:uint64-be
(name_length:uint64-be | name:ascii | value_length:uint64-be | value:raw-bytes)...
```

Encoding rules:

- Preserve raw filesystem path bytes; never normalize Unicode.
- Encode integers as standard ASCII without leading zeros, except `sequence`, which is 20 digits.
- Encode booleans as `0`/`1`, hashes as lowercase hex, and absent values as literal `N/A`.
- Emit fields in schema order. Sort records bytewise by the declared key; reject duplicate keys.
- Hash each complete binary artifact with SHA-256. Bind its digest into the relevant subject.
- Store preimage and reverse-patch blobs by content hash only when exclusions permit their content.
- Never store secrets or unsafe file content. Report excluded evidence and block any mutation that depends on it.

Use endpoint IDs `left` and `right`. Use Git-root ID `shared` in project mode or `left`/`right` in repository mode. Project mode contains two `endpoint-topology` records and one `topology-git` record.

## 2. Record Schemas

Field order is normative.

| Kind | Sort key | Ordered fields |
|---|---|---|
| `control` | `physical-path` | `physical-path`, `device-id`, `mount-id`, `inode`, `file-type`, `mode`, `size`, `sha256` |
| `endpoint-topology` | `endpoint-id` | `run-id`, `common-parent`, `common-device-id`, `common-mount-id`, `common-inode`, `endpoint-id`, `endpoint-name`, `physical-root`, `device-id`, `mount-id`, `inode`, `ancestor-chain-sha256`, `owning-git-root-id` |
| `topology-git` | `git-root-id` | `run-id`, `git-root-id`, `git-top-level`, `topology`, `head`, `ref`, `index-sha256`, `status-sha256`, `refs-sha256` |
| `git-admin-state` | `git-root-id`, `admin-path` | `run-id`, `git-root-id`, `admin-path`, `presence`, `device-id`, `mount-id`, `inode`, `file-type`, `mode`, `size`, `sha256` |
| `scope-input-action` | `root-id`, `path`, `sequence` | `run-id`, `subscope-id`, `sequence`, `root-id`, `path`, `peer-root-id`, `peer-path`, `presence`, `ancestor-chain-sha256`, `device-id`, `mount-id`, `inode`, `file-type`, `mode`, `size`, `sha256`, `git-state`, `dirty-adoption`, `drift-class`, `operation`, `output-presence`, `output-type`, `output-mode`, `output-sha256` |
| `preimage-rollback` | `root-id`, `path`, `sequence` | `run-id`, `subscope-id`, `sequence`, `root-id`, `path`, `preimage-presence`, `preimage-type`, `preimage-mode`, `preimage-sha256`, `preimage-blob-sha256`, `reverse-patch-sha256` |
| `postimage` | `root-id`, `path` | `run-id`, `subscope-id`, `root-id`, `path`, `presence`, `ancestor-chain-sha256`, `device-id`, `mount-id`, `inode`, `file-type`, `mode`, `size`, `sha256`, `verification`, `residual-class` |
| `residual-drift` | `root-id`, `path` | `run-id`, `subscope-id`, `root-id`, `path`, `relation`, `decision`, `variance-id`, `risk` |
| `acceptance-git` | `git-root-id` | `run-id`, `subscope-id`, `git-root-id`, `accepted-base-head`, `accepted-base-ref`, `index-sha256`, `dirty-sha256`, `disjoint-state-sha256` |
| `preapply-review` | `subscope-id` | `run-id`, `subscope-id`, `review-revision`, `reviewed-preview-sha256`, `scope-input-action-sha256`, `preimage-rollback-sha256`, `rollback-plan-sha256`, `verdict`, `critical-count`, `major-count`, `findings-sha256`, `review-report-sha256` |
| `baseline-selector` | `record-kind`, `left`, `right` | `record-kind`, `left`, `right` |
| `baseline-store-key` | `selector-id` | `selector-id`, `common-parent`, `common-device-id`, `common-mount-id`, `common-inode`, `left-physical-root`, `left-device-id`, `left-mount-id`, `left-inode`, `right-physical-root`, `right-device-id`, `right-mount-id`, `right-inode`, `topology`, `direction`, `baseline-selector-sha256` |
| `baseline-checkpoint` | `root-id`, `path` | `run-id`, `baseline-store-key-sha256`, `endpoint-topology-sha256`, `topology-git-sha256`, `direction`, `scope-input-action-sha256`, `residual-drift-sha256`, `final-verification-sha256`, `origin`, `origin-commit`, `root-id`, `path`, `presence`, `file-type`, `mode`, `sha256` |

## 3. Approval Subjects

Use UTF-8, LF endings, and a final LF. Hash the exact bytes. These subjects bind canonical evidence; the shared lifecycle owns the Approval Record and envelope.

Apply subject:

```text
format="DRN-SYNC-APPLY-SUBJECT/1"
run_id="..."
subscope_id="SS-NNN"
direction="..."
endpoint_topology_sha256="..."
topology_git_sha256="..."
control_sha256="..."
scope_input_action_sha256="..."
baseline_checkpoint_sha256="..."
preimage_rollback_sha256="..."
preview_sha256="..."
rollback_plan_sha256="..."
preapply_review_sha256="..."
priority_stack_decision="..."
residual_risk="..."
```

Set `approval_subject_sha256` to the subject hash and `approval_preview_sha256` to the raw preview hash.

Acceptance subject:

```text
format="DRN-SYNC-ACCEPTANCE-SUBJECT/1"
run_id="..."
subscope_id="SS-NNN"
endpoint_topology_sha256="..."
topology_git_sha256="..."
control_sha256="..."
postimage_sha256="..."
residual_drift_sha256="..."
acceptance_git_sha256="..."
priority_stack_decision="..."
residual_risk="..."
```

Set `approval_subject_sha256` to the acceptance-subject hash. Apply and acceptance records are separate and append-only.

## 4. Reusable Baseline Store

Normalize the invocation into `baseline-selector` records: substitute endpoint IDs `left`/`right`, normalize `only`, preserve explicit path pairs, and default `map` to `same-relative-paths`. Generate one `baseline-store-key` record with selector ID `pair`.

The stable store key binds physical common-parent and endpoint identities, topology, direction, and normalized selector digest. It excludes Run ID, timestamps, HEAD/refs, index/status, and enumerated file lists.

Open every component descriptor-relative without following links. Endpoint names never enter store paths.

```text
.agent/temp/SYNC-BASELINES/<store-key-sha256>/
  versions/<baseline-checkpoint-sha256>.bin
  variances/<residual-drift-sha256>.bin
  current
  promotion.lock
```

`current` is UTF-8 with LF endings and a final LF:

```text
format="DRN-SYNC-BASELINE-CURRENT/1"
baseline_sha256="<lowercase-hex>"
variance_sha256="<lowercase-hex>"
```

Select a version only through `current`. An absent key directory or pointer means no baseline. Reject malformed pointers, missing companions, digest mismatch, or checkpoint evidence that does not match the normalized invocation.

Promote only after `/sync-execute` proves the complete final scope. Acquire `promotion.lock` exclusively, revalidate the prior pointer, write immutable companions durably, and conditionally replace `current`. Sync the selector directory before releasing the lock. Concurrent pointer change aborts promotion.
