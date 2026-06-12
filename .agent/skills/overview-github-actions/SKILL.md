---
name: overview-github-actions
description: Use when reviewing or modifying GitHub Actions workflows, composite actions, CI gates, release pipelines, Docker publishing, package publishing, or security scanning.
last-updated: 2026-06-12
difficulty: intermediate
tokens: ~0.8K
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
- Pin third-party actions according to repository policy. When the profile requires full commit SHAs with version comments, verify both the SHA and the explanatory version comment.
- Protect `.github/workflows/**`, `.github/actions/**`, and `CODEOWNERS` with code-owner review.
- Gate publishing credentials behind build/test/security-scan success.
- Upload SARIF or code-scanning results when the repository uses supported scanners.

## Composite Actions

Composite actions reduce duplication, but they are part of the CI control plane. Review them like production code:

- Inputs are validated and quoted.
- Secrets are passed only to steps that need them.
- Working directories are explicit.
- Tool versions come from profile, lockfiles, or setup actions.
- Outputs are documented and consumed by downstream jobs deliberately.

## Release Pipeline Checks

- Version is derived from the repository's tag or package metadata.
- Build artifacts used for packaging are the same artifacts that were scanned or tested.
- Docker images are built with SBOM/provenance when supported.
- Package publishing happens after security gates, not before.
- Dependency-only upgrades are not listed in release notes unless they are breaking or user-facing.

## Docker Standards

When Docker publishing is in scope, verify:

- Dockerfile paths and image names come from the repository profile or workflow files.
- Multi-arch settings match the supported runtime targets.
- Base images are pinned or governed by a documented update policy.
- CVE gates match the repository's risk tolerance.
- Promotion reuses scanned image digests when the pipeline supports it.

## Required Secrets

Document required secrets in the repository profile or deployment docs, not generic skills. Keep names, scopes, and consuming workflows synchronized.

## Related Skills

- [basic-git-conventions](../basic-git-conventions/SKILL.md) - Branching, tagging, and release-note conventions.
- [basic-security-checklist](../basic-security-checklist/SKILL.md) - Security review triggers.
