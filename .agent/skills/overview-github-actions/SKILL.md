---
name: overview-github-actions
description: CI/CD and deployment - GitHub Actions workflows (develop, master, release), composite actions, Docker multi-arch publishing with SBOM/provenance, NuGet package publishing, and security scanning (SonarCloud, CodeQL). Complete deployment pipeline documentation. Keywords: cicd, github-actions, deployment, docker, nuget, security-scanning, sonarcloud, codeql, release-management, continuous-integration
last-updated: 2026-06-07
difficulty: intermediate
tokens: ~0.5K
---

# Deployment

CI/CD pipeline, GitHub Actions structure, and Docker containerization for `DRN-Project`.

## 1. Workflow Strategy

| Branch / Tag | Workflow | Purpose | Triggers |
| :--- | :--- | :--- | :--- |
| `develop` | `develop.yml` | **Fast CI**: Parallel frontend audit/build and backend .NET build/tests, joined by `build` gatekeeper | Push/PR to `develop` |
| `master` | `master.yml` | **Quality Gate**: Parallel frontend audit/build, SonarCloud quality gate, and CodeQL, joined by `gatekeeper` | Push/PR to `master`, Schedule (Sunday) |
| `release/v*.*.*` | `release.yml` | **Release CD**: NuGet + Docker publish | Push of tag `release/v*` |
| `release/v*.*.*-previewNNN` | `release-preview.yml` | **Preview CD**: Pre-release NuGet + Docker publish | Push of fixed-width preview tag, e.g. `release/v1.2.3-preview001` |

## 2. GitHub Actions Architecture

**Composite Actions** in `.github/workflows/actions/` reduce duplication:

### CI Job Parallelism

- **`develop.yml`**: `frontend` and `backend` run independently; final `build` job verifies both results to preserve an aggregate CI status.
- **`master.yml`**: `frontend`, `build-and-sonar-scan`, and `codeql-scan` run independently; `gatekeeper` verifies all three results.
- **Release workflows** stay single-job unless explicit artifact handoff is added, because NuGet packing uses `--no-build` outputs and Docker images consume `Sample.Hosted/wwwroot` from the build context.

### Core Actions

- **`setup-sdk-and-tools`**: .NET SDK setup, tool restoration, caching
- **`frontend-build`**: Node 24 setup, `npm ci`, high-severity npm audit, Vite asset build
- **`dotnet-build` / `dotnet-build-release`**: Centralized build logic
- **`dotnet-test` / `dotnet-test-release`**: Centralized testing logic
- **`docker-publish-all`**: Orchestrates all Docker image publishing
- **`nuget-publish-all`**: Orchestrates all NuGet package publishing

### Security & Analysis Actions

- **`sonar-begin` / `sonar-end`**: SonarCloud static analysis
- **`scan-nuget-vulnerabilities`**: Dependency vulnerability checking
- **Docker Scout**: Release image CVE gate with SARIF upload

## 3. Docker Standards

Built via `docker/build-push-action`.

| Config | Value |
|--------|-------|
| **Architectures** | `linux/amd64`, `linux/arm64` |
| **SBOM** | Generated (`sbom: true`) |
| **Provenance** | SLSA (`provenance: true`) |
| **Scanner** | Docker Scout (`quickview,cves,recommendations`) fails on `critical,high,medium,low` CVEs |
| **Registry** | Docker Hub `duranserkan` namespace |
| **Tags** | `semver`, stable-only `major.minor`, `branch`, `pr` |

### Dockerfile Locations

- `DRN.Nexus.Hosted/Dockerfile` → `drn-project-nexus`
- `Sample.Hosted/Dockerfile` → `drn-project-sample`

## 4. NuGet Publishing

- **Versioning**: Extracted from Git tag
- **Attestation**: `actions/attest`
- **Artifacts**: Uploaded as `packages` workflow artifacts
- **Published packages**: `SharedKernel`, `Utils`, `EntityFramework`, `Hosting`, `Testing`
- **Planned packages**: `Jobs` and `MassTransit` build with the solution but are not release-published yet

## 5. Required Secrets

| Secret | Purpose |
|--------|---------|
| `SONAR_TOKEN` | SonarCloud analysis |
| `NUGET_TOKEN` | NuGet.org publishing |
| `DOCKER_USERNAME` | Docker Hub username |
| `DOCKER_PASSWORD` | Docker Hub access token |

## Related Skills

- [basic-git-conventions](../basic-git-conventions/SKILL.md) — Branching, tagging, conventions
