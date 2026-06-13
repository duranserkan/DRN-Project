---
description: Canonical specification for the Baseline Inputs Hash — staleness gate for /update plans
---

## Baseline Inputs Hash Specification

The Baseline Inputs Hash is a deterministic SHA-256 digest that gates plan staleness in `/update` workflows.

### Path Normalization

1. Trim surrounding whitespace.
2. Use forward slashes (`/`) as separators.
3. Resolve `.` and `..` path segments without leaving the repository root.
4. Serialize as a repository-root relative normalized path.
5. No trailing slash for files.

### File Content Hashing

- Compute SHA-256 over raw bytes (treat all files as binary).
- Do not normalize line endings, timestamps, or file contents.
- Include the repository-tracked Git file mode for tracked files, using `git ls-files -s -- <path>` mode values such as `100644`, `100755`, or `120000`.
- Use `mode=untracked` only for material input files that are not tracked by Git.

### Deletion Markers

- Represent each deleted file as `DELETE:<normalized-path>`.

### Input Serialization

1. Collect one entry per in-scope file:
   - Existing tracked file: `<normalized-path>:mode=<git-file-mode>:sha256=<hex-sha256>`
   - Existing untracked material file: `<normalized-path>:mode=untracked:sha256=<hex-sha256>`
   - Deleted file: `DELETE:<normalized-path>`
2. Use lowercase hexadecimal for all SHA-256 values.
3. UTF-8 encode each entry and sort by encoded bytes in ascending byte order (case-sensitive).
4. Concatenate sorted entry bytes with exactly one LF byte (`0x0A`, `\n`) between entries and no trailing newline.
5. Compute SHA-256 of the concatenated byte sequence to produce the Baseline Inputs Hash.

### Hash Requirement

Compute the hash for every material in-scope input:

- Existing files that influence discovery, planning, execution, or verification.
- Deleted files represented by deletion markers.
- Renamed files, including renamed-from paths when they affect drift detection.

Use `N/A` only when the resolved scope has no material input files to hash. Record the exact plan header value `Baseline Inputs Hash Justification: no-material-input-files`.

### N/A Fallback

When `N/A`, the staleness guard does not compare hashes. Continue only after confirming the plan header contains exactly `Baseline Inputs Hash Justification: no-material-input-files` and re-checking exact scope paths still finds no material inputs; otherwise abort as stale.
