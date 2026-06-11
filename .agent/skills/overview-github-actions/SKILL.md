---
name: overview-github-actions
description: "CI/CD and deployment - GitHub Actions workflows (develop, pull-request, master, release), composite actions, Docker multi-platform publishing with SBOM/provenance, NuGet package publishing, and security scanning (SonarCloud, CodeQL). Complete deployment pipeline documentation. Keywords: cicd, github-actions, deployment, docker, nuget, security-scanning, sonarcloud, codeql, release-management, continuous-integration"
last-updated: 2026-06-11
difficulty: intermediate
tokens: ~0.5K
---

# Deployment

CI/CD pipeline, GitHub Actions structure, and Docker containerization for `DRN-Project`.

## 1. Workflow Strategy

| Branch / Tag | Workflow | Purpose | Triggers |
| :--- | :--- | :--- | :--- |
| `develop` | `develop.yml` | **Develop Baseline**: Parallel frontend audit/build and backend .NET build/tests, joined by `gatekeeper` | Push to `develop` |
| PR to `develop` / `master` | `pull-request.yml` | **PR Quality Gate**: Secretless frontend audit/build, backend build/tests, and CodeQL, joined by `gatekeeper` | Pull request to `develop` or `master` |
| `master` | `master.yml` | **Master Baseline**: Parallel frontend audit/build, SonarCloud branch quality gate, and CodeQL, joined by `gatekeeper` | Push to `master`, Schedule (Sunday) |
| `release/v*.*.*` | `release.yml` | **Release CD**: Release build/test, SonarCloud quality gate, CodeQL, Docker digest scan gate, then NuGet publish + Docker tag promotion | Push of tag `release/v*` |
| `release/v*.*.*-previewNNN` | `release-preview.yml` | **Preview CD**: Release build/test, SonarCloud quality gate, CodeQL, Docker digest scan gate, then pre-release NuGet publish + Docker tag promotion | Push of fixed-width preview tag, e.g. `release/v1.2.3-preview001` |

## 2. GitHub Actions Architecture

**Composite Actions** in `.github/actions/` reduce duplication.

### CI Job Parallelism

- **`develop.yml`**: `frontend` and `backend` run independently; final `gatekeeper` job verifies both results to preserve an aggregate CI status.
- **`pull-request.yml`**: `frontend`, `backend`, and `codeql` run independently for PRs to `develop` and `master`; `gatekeeper` verifies all three results. This workflow uses `pull_request`, never `pull_request_target`; it checks out trusted CI code from the event base SHA at the workspace root and PR source under `src`, then passes `working-directory: src` into trusted composite actions. It intentionally stays secretless because PR code executes during build and test steps, and every PR job has an explicit timeout to cap hostile or accidental runner exhaustion.
- **`master.yml`**: `frontend`, `backend`, and `codeql` run independently for branch/scheduled scans; `gatekeeper` verifies all three results. The `backend` job owns SonarCloud branch analysis and coverage.
- **Release workflows** stay single-job (`publish`) unless explicit artifact handoff is added, because NuGet packing uses `--no-build` outputs and Docker images consume `Sample.Hosted/wwwroot` from the build context. They run frontend build, Sonar begin, shared CodeQL Release build, Release coverage tests, Sonar end, Docker digest staging, and Docker Scout scanning before NuGet publishing and Docker tag promotion. A temporary `staged-<version>-run-<run_id>-<attempt>` tag is pushed with the staged digest and retained for manual inspection or cleanup.

### PR CI Guards

- Split checkout protects PR-run-time execution by keeping trusted composite actions from the event base SHA at the workspace root while PR source lives under `src`.
- `.github/CODEOWNERS` protects merge-time CI control-plane changes by owning itself, `.github/workflows/**`, and `.github/actions/**`; repository rulesets/branch protection must require code-owner review for these paths.
- PR merge protection should require the aggregate `pull-request / gatekeeper` status and GitHub code scanning results for CodeQL.

### Core Actions

- **`setup-sdk-and-tools`**: .NET SDK setup, tool restoration, caching
- **`frontend-build`**: Node 24 setup, `npm ci`, high-severity npm audit, Vite asset build
- **`dotnet-build`**: Centralized build logic with selectable configuration and optional version input
- **`dotnet-test` / `dotnet-test-coverage`**: Centralized testing logic with selectable configuration where needed
- **`dotnet-validate`**: Debug build/test wrapper for develop and PR backend jobs
- **`dotnet-sonar-scan`**: Protected-branch SonarCloud build, coverage, and quality-gate wrapper
- **`docker-release-metadata`**: Derives Docker release tags and labels from the workflow-extracted release version and configured image repository prefix
- **`docker-stage-scan`**: Stages release images by digest, scans the staged digest, and uploads SARIF
- **`docker-promote`**: Validates an already scanned Docker digest and promotes human-facing tags from that immutable digest
- **`docker-stage-scan-all`**: Sets up Docker Buildx/login and stages/scans all release Docker images, outputting scanned digests
- **`docker-promote-all`**: Sets up Docker Buildx/login and promotes all release Docker tags from scanned digests
- **`nuget-publish-all`**: Orchestrates all NuGet package publishing

### Security & Analysis Actions

- **`sonar-begin` / `sonar-end`**: SonarCloud static analysis for protected branch push/scheduled and release tag workflows
- **`codeql-scan`**: CodeQL init/build/analyze wrapper for PR, protected-branch, and release scans
- **`scan-nuget-vulnerabilities`**: Dependency vulnerability checking
- **Docker Scout**: Release image CVE gate with SARIF upload before NuGet publishing and Docker tag promotion

## 3. Docker Standards

Built via `docker/build-push-action`, scanned by staged digest, then promoted by digest after the gate.

| Config | Value |
|--------|-------|
| **Architectures** | Configurable `platforms` action input; default `linux/amd64`, `linux/arm64` |
| **SBOM** | Generated (`sbom: true`) |
| **Provenance** | SLSA (`provenance: true`) |
| **Scanner** | Docker Scout (`quickview,cves`) scans the staged digest and fails on `critical,high,medium,low` CVEs |
| **Registry** | Release workflows define `DOCKER_REGISTRY=docker.io` for Docker login and `DOCKER_IMAGE_REPOSITORY_PREFIX=duranserkan` as the image namespace/path. Docker actions compose image names as `DOCKER_REGISTRY/DOCKER_IMAGE_REPOSITORY_PREFIX/<image-name>`. |
| **Tags** | Derived from the workflow-extracted release version; `semver`, stable-only `major.minor`, no implicit `latest` |
| **Staging tag** | Temporary `staged-<version>-run-<run_id>-<attempt>` tag pushed with the staged digest and retained for manual inspection or cleanup |

### Dockerfile Locations

- `DRN.Nexus.Hosted/Dockerfile` → `drn-project-nexus`
- `Sample.Hosted/Dockerfile` → `drn-project-sample`

## 4. NuGet Publishing

- **Versioning**: Extracted from Git tag and passed to publish actions as an explicit input
- **Package source**: Release workflows define `NUGET_PACKAGE_SOURCE_URL=https://api.nuget.org/v3/index.json` and pass it to NuGet publish actions
- **Attestation**: `actions/attest`
- **Artifacts**: Packed under `artifacts/packages` and uploaded as `packages` workflow artifacts
- **Credential scope**: NuGet token is passed only to post-gate package push steps
- **Published packages**: `SharedKernel`, `Utils`, `EntityFramework`, `Hosting`, `Testing`
- **Planned packages**: `Jobs` and `MassTransit` build with the solution but are not release-published yet

## 5. Required Secrets

| Secret | Purpose |
|--------|---------|
| `SONAR_TOKEN` | SonarCloud analysis |
| `NUGET_TOKEN` | NuGet.org publishing |
| `DOCKER_USERNAME` | Docker registry username for `DOCKER_REGISTRY` |
| `DOCKER_PASSWORD` | Docker registry password or access token for `DOCKER_REGISTRY` |

## Related Skills

- [basic-git-conventions](../basic-git-conventions/SKILL.md) — Branching, tagging, conventions
