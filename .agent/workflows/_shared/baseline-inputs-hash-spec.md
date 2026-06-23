---
description: Canonical Baseline Inputs Hash spec for /update staleness gates
---

## Baseline Inputs Hash

Use this deterministic SHA-256 digest for `/update` plan staleness.

### Normalize Paths

1. Trim surrounding whitespace.
2. Replace separators with `/`.
3. Resolve `.` and `..` without escaping repository root.
4. Serialize the repository-root relative path.
5. Omit trailing slash for files.

### Hash File Content

- Hash raw bytes; treat every file as binary.
- Do not normalize content, line endings, or timestamps.
- For tracked files, include Git mode from `git ls-files -s -- <path>` (`100644`, `100755`, `120000`).
- Use `mode=untracked` only for material untracked inputs.

### Mark Deletions

- Serialize each deleted file as `DELETE:<normalized-path>`.

### Serialize Inputs

1. Collect one entry per in-scope file:
   - Tracked file: `<normalized-path>:mode=<git-file-mode>:sha256=<hex-sha256>`
   - Untracked material file: `<normalized-path>:mode=untracked:sha256=<hex-sha256>`
   - Deleted file: `DELETE:<normalized-path>`
2. Use lowercase hex SHA-256.
3. UTF-8 encode entries; sort encoded bytes ascending, case-sensitive.
4. Join entries with one LF byte (`0x0A`, `\n`); omit trailing newline.
5. SHA-256 hash the joined bytes.

### Required Scope

Hash every material in-scope input:

- Existing files that influence discovery, planning, execution, or verification.
- Deleted files through deletion markers.
- Renamed files, including renamed-from paths when needed for drift detection.

Use `N/A` only when the resolved scope has no material inputs. Then record this exact plan header:

`Baseline Inputs Hash Justification: no-material-input-files`

### N/A Guard

When hash is `N/A`, skip comparison only after both checks pass:

1. Plan header contains exactly `Baseline Inputs Hash Justification: no-material-input-files`.
2. Exact scope paths still contain no material inputs.

Otherwise abort as stale.
