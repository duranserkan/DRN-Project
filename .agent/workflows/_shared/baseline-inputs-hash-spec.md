---
description: Canonical Baseline Inputs Hash spec for /update staleness gates
---

## Baseline Inputs Hash

Use the SHA-256 of the canonical binary input manifest below for `/update` plan staleness. Persist the exact manifest at `.agent/temp/update-baseline-inputs.manifest`; a material-input plan without that file is stale.

### Resolve Raw Paths

1. Use repository-root-relative path bytes. Never trim whitespace or apply Unicode, case, or text normalization.
2. For tracked and deleted paths, use raw NUL-delimited Git output such as `git ls-files -z` or `git diff --name-status -z`; never parse quoted display output. For discovered untracked paths, join raw relative path components with `/`.
3. Resolve `.` and `..` structurally during discovery; reject absolute paths, root escapes, directories used as file records, and paths containing NUL.
4. Include renamed-from and renamed-to paths as separate deletion/current records when both affect drift detection.

### Hash File Content

- Inspect each existing working-tree path with a non-following metadata operation before hashing. Abort if its type changes during the read; never dereference a symlink.
- Hash current working-tree bytes, not index/blob content, and treat every regular file as binary.
- Do not normalize content, line endings, or timestamps.
- For tracked regular files, use record kind `F` and current canonical mode `100755` when any executable bit is set or `100644` otherwise. Do not reuse a stale index mode.
- For tracked symlinks, use record kind `L`, mode `120000`, and hash the current raw `readlink` payload. Never use the link payload stored in the Git index as current-content evidence.
- Classify tracked entries by their current working-tree type, so regular-file/symlink transitions change the record kind and mode.
- For untracked symlinks, hash the raw link-target bytes without dereferencing. Use mode `0` for every untracked entry.
- Deletions have mode `0` and a 32-byte all-zero content digest.

### Canonical Binary Manifest

Start the manifest with ASCII bytes `DRN-UPDATE-BASELINE`, followed by NUL and version byte `0x01`.

Collect one record per material path, sort by raw path bytes ascending and then record kind, and append records without separators:

```text
kind:1 | mode:uint32-be | path_length:uint64-be | path:raw-bytes | content_sha256:32
```

Record kinds are `F` tracked regular file, `L` tracked symlink, `U` untracked regular file, `S` untracked symlink, and `D` deletion. Numeric fields are unsigned big-endian integers; parse Git's six-digit mode as octal before serialization. Use mode `0` for untracked entries and deletions. The length-prefixed raw path preserves every valid Git filename, including whitespace, newlines, and non-UTF-8 bytes, without aliases.

`Baseline Inputs Hash` is the lowercase hexadecimal SHA-256 of the complete manifest bytes. The manifest is the canonical input list: staleness validation must reproduce both the current material-path set and every record, then compare the resulting manifest hash.

### Required Scope

Hash every material in-scope input:

- Existing files that influence discovery, planning, execution, or verification.
- Deleted files through deletion markers.
- Renamed files, including renamed-from paths when needed for drift detection.

Use `N/A` only when the resolved scope has no material inputs. Then record this exact plan header:

`Baseline Inputs Hash Justification: no-material-input-files`

For `N/A`, set `Baseline Inputs Manifest: N/A` and do not retain a manifest from an earlier plan.

### N/A Guard

When hash is `N/A`, skip comparison only after all checks pass:

1. Plan header contains exactly `Baseline Inputs Hash Justification: no-material-input-files`.
2. Plan header contains exactly `Baseline Inputs Manifest: N/A`.
3. Exact scope paths still contain no material inputs.

Otherwise abort as stale.
