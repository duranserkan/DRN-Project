# Solution Structure

[DRN-Project](../README.md) organizes its projects and supporting files through [DRN.slnx](../DRN.slnx). The solution folders below are logical IDE groups. Physical project directories live at the repository root.

## Repository areas

1. **Docker:** Standalone dependency stacks and container configuration in `Docker/`. See the [Docker and Docker Compose guide](../Docker/README.md).
2. **Docs:** Project documentation in root Markdown files and `docs/`.
3. **Items:** Shared repository configuration, including `.gitignore`, `.dockerignore`, build settings, `.agent/`, and `.github/` workflows and actions.
4. **Src:** Three groups of source projects: Framework, Nexus, and Sample, described below.
5. **Test:** .NET unit, analyzer, integration, and performance tests in `DRN.Test.*`, shared test support, and `DRN.MultiApp.Testing`. Frontend tests live with the Sample application. See the [frontend test guide](../Sample.Hosted/README.md#frontend-tests).
6. **docker-compose:** The local application stack for `Sample.Hosted` and its dependencies. The [Compose project](../docker-compose.dcproj) belongs to the Docker solution group and references the root [Compose configuration](../docker-compose.yml).

## Source projects

### Framework

The `DRN.Framework.*` projects provide reusable NuGet packages for compatible .NET applications. They are grouped under `/Src/Framework/` in the solution. Package-specific documentation lives in each module's README and release notes.

### Nexus

The `DRN.Nexus.*` projects are grouped under `/Src/Nexus/`. Nexus is intended to connect services and APIs developed with DRN.Framework through a unified web application, API, and background services.

Planned capabilities include:

- [ ] **Configuration Management:** Provide configuration to connected microservices.
- [ ] **Migration Management:** Manage microservice database migrations, including sharding and schema-based multitenancy.
- [ ] **Service Discovery:** Disclose the microservice network.
- [ ] **Observability, Documentation and Monitoring Hub:** Visualize service workflows and related information.

See the [roadmap](../ROADMAP.md#drnnexus) for Nexus release planning.

### Sample

The `Sample.*` projects are grouped under `/Src/Sample/`. The reference application demonstrates DRN.Framework usage for testing, presentation, and documentation, with intended Nexus connectivity. See the [Sample application guide](../Sample.Hosted/README.md) for local startup, developer tools, error handling, notifications, and frontend work.

For prerequisites, package installation, and solution build and test commands, see [Quick Start](quick-start.md).
