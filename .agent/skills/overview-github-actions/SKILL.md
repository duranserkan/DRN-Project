---
name: overview-github-actions
description: CI/CD and deployment - GitHub Actions workflows (develop, master, release), composite actions, Docker multi-arch publishing with SBOM/provenance, NuGet package publishing, and security scanning (SonarCloud, CodeQL). Complete deployment pipeline documentation. Keywords: cicd, github-actions, deployment, docker, nuget, security-scanning, sonarcloud, codeql, release-management, continuous-integration
last-updated: 2026-02-15
difficulty: intermediate
---

# Deployment

CI/CD pipeline, GitHub Actions structure, and Docker containerization for `DRN-Project`.

## 1. Workflow Strategy

| Branch / Tag | Workflow | Purpose | Triggers |
| :--- | :--- | :--- | :--- |
| `develop` | `develop.yml` | **Fast CI**: Build and test | Push/PR to `develop` |
| `master` | `master.yml` | **Quality Gate**: SonarCloud, CodeQL, strict gates | Push/PR to `master`, Schedule (Sunday) |
| `v*.*.*` | `release.yml` | **Release CD**: NuGet + Docker publish | Push of tag `v*` |
| `v*-preview*` | `release-preview.yml` | **Preview CD**: Pre-release NuGet publish | Push of tag `v*-preview*` |

## 2. GitHub Actions Architecture

**Composite Actions** in `.github/workflows/actions/` reduce duplication:

### Core Actions

- **`setup-sdk-and-tools`**: .NET SDK setup, tool restoration, caching
- **`dotnet-build` / `dotnet-build-release`**: Centralized build logic
- **`dotnet-test` / `dotnet-test-release`**: Centralized testing logic
- **`docker-publish-all`**: Orchestrates all Docker image publishing
- **`nuget-publish-all`**: Orchestrates all NuGet package publishing

### Security & Analysis Actions

- **`sonar-begin` / `sonar-end`**: SonarCloud static analysis
- **`scan-file-system-vulnerabilities`**: Filesystem vulnerability scanning
- **`scan-nuget-vulnerabilities`**: Dependency vulnerability checking

## 3. Docker Standards

Built via `docker/build-push-action`.

| Config | Value |
|--------|-------|
| **Architectures** | `linux/amd64`, `linux/arm64` |
| **SBOM** | Generated (`sbom: true`) |
| **Provenance** | SLSA (`provenance: true`) |
| **Scanner** | Docker Scout (`quickview,cves,recommendations`) |
| **Registry** | Docker Hub `duranserkan` namespace |
| **Tags** | `semver`, `major.minor`, `branch`, `pr` |

### Dockerfile Locations

- `DRN.Nexus.Hosted/Dockerfile` → `drn-project-nexus`
- `Sample.Hosted/Dockerfile` → `drn-project-sample`

## 4. NuGet Publishing

- **Versioning**: Extracted from Git tag
- **Attestation**: `actions/attest-build-provenance`
- **Artifacts**: Uploaded as `packages` workflow artifacts

## 5. Required Secrets

| Secret | Purpose |
|--------|---------|
| `SONAR_TOKEN` | SonarCloud analysis |
| `NUGET_TOKEN` | NuGet.org publishing |
| `DOCKER_USERNAME` | Docker Hub username |
| `DOCKER_PASSWORD` | Docker Hub access token |

## Related Skills

- [basic-git-conventions](../basic-git-conventions/SKILL.md) — Branching, tagging, conventions
