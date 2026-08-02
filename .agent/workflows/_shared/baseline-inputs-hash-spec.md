---
description: Baseline and output hashing for /update staleness gates
---

## Shared Path-State Snapshot

Resolve one exact path set in deterministic order. The plan and Semantic Plan
SHA-256 bind that set; changing it invalidates the snapshot. For an empty set, never
invoke `git status` or `git diff`: Approved Output Preimages and Output Revision create an explicit zero-byte status artifact and hash it. Baseline Inputs `N/A` Guard creates no artifacts.

For a non-empty set, pass literal operands safely without globs or word splitting.
For every path:
- Require a UTF-8, repository-relative, control-character-free path. Reject CR, LF, NUL, absolute paths, and repository escapes during scope resolution.
- Existing paths must be single-link regular files. Reject symlinks (`test -L`), directories used as file records, and special files.

Approved output transitions record canonical mode as `100755` when executable bit is set and `100644` otherwise. Baseline Inputs Hash and Output Revision SHA-256 bind path presence and content, not mode; mode is an immediate apply/resume safety check.

Persist two artifacts per snapshot:
1. Raw NUL-delimited status: `git status --porcelain=v1 -z --untracked-files=all`.
2. Standard `shasum` manifest over the status artifact and existing regular files.

## Baseline Inputs Hash

Use these paths:
- `.agent/temp/update-baseline-status.z`
- `.agent/temp/update-baseline-inputs.manifest`

Declared Stage 1-5 outputs are excluded because the approved output preimage contract below binds them. Create the artifacts from the repository root:

```bash
git status --porcelain=v1 -z --untracked-files=all -- <input-pathspecs> > .agent/temp/update-baseline-status.z
shasum -a 256 -b -- .agent/temp/update-baseline-status.z <existing-regular-input-files> > .agent/temp/update-baseline-inputs.manifest
shasum -a 256 -b -- .agent/temp/update-baseline-inputs.manifest
```

The final digest is `Baseline Inputs Hash`. Preserve `Baseline HEAD` as part of the binding. Revalidation recomputes status and verifies the manifest:

```bash
git status --porcelain=v1 -z --untracked-files=all -- <input-pathspecs> > .agent/temp/update-baseline-status.current.z
cmp -s .agent/temp/update-baseline-status.z .agent/temp/update-baseline-status.current.z
shasum -a 256 -c .agent/temp/update-baseline-inputs.manifest
shasum -a 256 -b -- .agent/temp/update-baseline-inputs.manifest
```

Require the same `HEAD`, every command to succeed independently, successful status comparison, and exact digest before and after manifest consumption. Otherwise the plan is stale.

## Approved Output Preimages

Before Apply approval, record one exact transition per Stage 1-5 output path in `.agent/temp/update-apply-preview.md`. The preview's raw-byte SHA-256 binds the complete tuple list.

Encode output transitions as unambiguous JSON objects (one per line):

```json
{"path": "docs/example.md", "pre": {"presence": 0, "mode": "0", "sha256": "N/A"}, "post": {"presence": 1, "mode": "100644", "sha256": "<raw-byte-sha256>"}}
```

Use `presence: 0`, `mode: "0"`, `sha256: "N/A"` for a missing side. A present side must have `presence: 1`, `mode: "100644"` or `"100755"`, and `<raw-byte-sha256>`. Record every preimage and postimage even when a proposed diff exists.

The tuple list is the sole output-preimage binding; no separate output-preimage snapshot or manifest exists.

Immediately before the first write to each pending output, verify its exact approved preimage. On resume, an output matching its preimage is pending; one matching its postimage is completed.

## Output Revision Hash

After execution, create the shared snapshot with these paths:
- `.agent/temp/update-verify-output-status.z`
- `.agent/temp/update-verify-outputs.manifest`

Generate the standard `shasum` manifest over the status artifact and existing regular output files. The final manifest digest is `Output Revision SHA-256`. Before using a cached result, revalidate status and require the recorded digest.

For an empty output set, use `Output Revision SHA-256` calculated from the empty status artifact. Output Revision is never `N/A`.

## N/A Guard

Use `Baseline Inputs Hash: N/A` only when the resolved non-output input scope has no material paths. Record:
- `Baseline Inputs Hash Justification: no-material-input-files`
- `Baseline Inputs Manifest: N/A`

Do not retain any baseline status or manifest artifact. Reconfirm empty scope before mutation.
