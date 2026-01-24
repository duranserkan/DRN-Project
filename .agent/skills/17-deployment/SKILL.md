---
description: Standardized GitHub Actions utilization for CI/CD, Docker publishing, and NuGet package management
---

# 17. Deployment

This skill documents the standardized deployment workflows and practices for the `DRN-Project`. It covers the CI/CD pipeline strategy, GitHub Actions structure, and Docker containerization standards.

## 1. Workflow Strategy

The project follows a GitFlow-inspired workflow adapted for continuous delivery:

| Branch / Tag | Workflow | Purpose | Triggers |
| :--- | :--- | :--- | :--- |
| `develop` | `develop.yml` | **Fast CI**: Quick feedback loop. Builds and tests the code. | Push/PR to `develop` |
| `master` | `master.yml` | **Quality Gate**: Comprehensive analysis. Includes SonarCloud scans, CodeQL security analysis, and stricter gates. | Push/PR to `master`, Schedule (Sunday) |
| `v*.*.*` | `release.yml` | **Release CD**: automated deployment. Publishes NuGet packages and Docker images to production registries. | Push of tag `v*` |
| `v*-preview*` | `release-preview.yml` | **Preview CD**: Similar to release but for pre-release versions. | Push of tag `v*-preview*` |

## 2. GitHub Actions Architecture

The repository uses **Composite Actions** to encapsulate logic and reduce duplication across workflows. These are located in `.github/workflows/actions/`.

### Core Composite Actions

-   **`setup-sdk-and-tools`**: Standardizes the .NET SDK setup, tool restoration, and caching.
-   **`dotnet-build` / `dotnet-build-release`**: centralized build logic.
-   **`dotnet-test` / `dotnet-test-release`**: Centralized testing logic.
-   **`docker-publish-all`**: Orchestrates the publishing of all Docker images in the solution.
-   **`nuget-publish-all`**: Orchestrates the publishing of all NuGet packages.

### Security & Analysis Actions

-   **`sonar-begin` / `sonar-end`**: Wraps SonarCloud static analysis.
-   **`scan-file-system-vulnerabilities`**: Scans the filesystem for known vulnerabilities.
-   **`scan-nuget-vulnerabilities`**: Checks dependencies against vulnerability databases.

## 3. Docker Standards

Docker images are built and published using the `docker/build-push-action`.

### Key Configuration

-   **Multi-Architecture Support**: Images are built for both `linux/amd64` and `linux/arm64`.
-   **Security**:
    -   **OMNI (SBOM)**: Software Bill of Materials is generated (`sbom: true`).
    -   **Provenance**: SLSA provenance is generated (`provenance: true`).
    -   **Docker Scout**: Integrated for vulnerability scanning (`quickview,cves,recommendations`).
-   **Registry**: Images are published to Docker Hub under the `duranserkan` namespace.
-   **Versioning**:
    -   `semver`: Matches the git tag (e.g., `1.2.3`).
    -   `major.minor`: Floating tag for stability (e.g., `1.2`).
    -   `branch`, `pr`: Context-aware tagging for non-release builds.

### Standard Dockerfile Locations

-   `DRN.Nexus.Hosted/Dockerfile` -> `drn-project-nexus`
-   `Sample.Hosted/Dockerfile` -> `drn-project-sample`

## 4. NuGet Publishing

NuGet package publishing is automated via the `release` workflows.

-   **Versioning**: extracted directly from the Git tag.
-   **Attestation**: Uses `actions/attest-build-provenance` to generate build provenance for packages.
-   **Artifacts**: Packages are uploaded as workflow artifacts (`packages`) for traceability.

## 5. Required Secrets

To function correctly, the repository requires the following secrets to be configured in GitHub:

-   `SONAR_TOKEN`: Token for SonarCloud analysis.
-   `NUGET_TOKEN`: API key for publishing to NuGet.org.
-   `DOCKER_USERNAME`: Docker Hub username.
-   `DOCKER_PASSWORD`: Docker Hub Access Token.
