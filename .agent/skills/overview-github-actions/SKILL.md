---
name: overview-github-actions
description: Use when reviewing or modifying GitHub Actions workflows, composite actions, CI gates, release pipelines, Docker publishing, package publishing, or security scanning.
last-updated: 2026-06-14
difficulty: intermediate
tokens: ~1.4K
---

# GitHub Actions And Deployment

> Portable CI/CD guidance. Load the repository profile for exact workflow names, branches, tags, registries, and publish targets.

## Workflow Strategy

Common workflow families:

| Family | Purpose |
|--------|---------|
| Pull request | Secretless validation for untrusted changes |
| Development branch | Baseline build, test, and quality gate |
| Protected branch | Deeper analysis, scheduled scans, or release readiness |
| Release tag | Build once, scan, publish packages/images, attest artifacts |
| Preview tag | Publish prerelease packages/images with fixed-width preview ordering |

## Security Rules

- Use `pull_request`, not `pull_request_target`, when PR code executes.
- Keep PR workflows secretless unless the workflow is carefully split between trusted and untrusted code.
- For split-checkout PR workflows, checkout trusted CI/control-plane code at the immutable event base SHA and checkout PR-controlled source into a separate path.
- Set explicit `timeout-minutes` on jobs that run untrusted or expensive code, and keep aggregate gatekeeper jobs short.
- Pin third-party actions according to repository policy. When the profile requires full commit SHAs with version comments, verify both the SHA and the explanatory version comment from the official upstream repository; annotated tags require the dereferenced tag commit.
- Protect `.github/workflows/**`, `.github/actions/**`, and `CODEOWNERS` with code-owner review.
- Gate publishing credentials behind build/test/security-scan success.
- Upload SARIF or code-scanning results when the repository uses supported scanners. If a custom SARIF verifier is used, missing, empty, malformed, or unparsable SARIF is a failed security gate; if platform code-scanning merge protection replaces it, require that protection in branch rulesets.
- Feed global project guidelines to review tools through their guideline-file mechanism instead of duplicating portable rules into long path instructions.

## Composite Actions

Composite actions reduce duplication, but they are part of the CI control plane. Review them like production code:

- Inputs are validated and quoted.
- Secrets are passed only to steps that need them.
- Working directories are explicit.
- Tool versions come from profile, lockfiles, or setup actions.
- Outputs are documented and consumed by downstream jobs deliberately.
- Dependency restore steps should not execute third-party lifecycle scripts unless a documented exception is required for production build correctness.

## Release Pipeline Checks

- Version is derived from the repository's tag or package metadata.
- Tag filters, version extraction guards, and package/image metadata rules agree on the same stable and preview tag shapes.
- Branch ancestry checks resolve exact refs and use `git merge-base --is-ancestor`; do not rely on branch list output or substring matching.
- Build artifacts used for packaging are the same artifacts that were scanned or tested.
- Parallelize only when generated-file boundaries are explicit. If package build output, frontend assets, or Docker context files cross job boundaries, add artifact upload/download or keep dependent steps in one job.
- Docker images are built with SBOM/provenance when supported.
- Package publishing and Docker tag promotion happen after security gates and staged-image CVE scans, not before.
- Registry and package publishing secrets are scoped to the steps or composite actions that need them.
- Dependency-only upgrades are not listed in release notes unless they are breaking, security-relevant, user-facing, or alter published package artifacts.

## Docker Standards

When Docker publishing is in scope, verify:

- Dockerfile paths and image names come from the repository profile or workflow files.
- Multi-arch settings match the supported runtime targets.
- Base images are pinned or governed by a documented update policy.
- CVE gates match the repository's risk tolerance.
- Promotion reuses scanned image digests when the pipeline supports it.
- Scanner inputs identify the exact image under review, preferably by repository plus staged digest, not runner-local defaults.
- CVE severity filters are explicit; include `unspecified` only when unknown-severity findings are intentionally release-blocking.
- Preview metadata must not overwrite stable tags such as `latest` or `major.minor`; derive Docker tags from the already-extracted release version.

## Required Secrets

Document required secrets in the repository profile or deployment docs, not generic skills. Keep names, scopes, and consuming workflows synchronized.

## Related Skills

- [basic-git-conventions](../basic-git-conventions/SKILL.md) - Branching, tagging, and release-note conventions.
- [basic-security-checklist](../basic-security-checklist/SKILL.md) - Security review triggers.
