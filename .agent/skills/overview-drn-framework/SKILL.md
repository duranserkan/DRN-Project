---
name: overview-drn-framework
description: "Route DRN work to version-matched package guidance, configuration and environment safeguards."
last-updated: 2026-11-13
difficulty: basic
---

# DRN.Framework Overview

## Documentation And Source References

For a NuGet consumer, start in the application project:

1. Identify the package and resolved version for the relevant target framework in `obj/project.assets.json` or `packages.lock.json`. Project files and `Directory.Packages.props` show requested versions.
2. In the assets file, combine a `packageFolders` root with the matching `libraries` entry's `path`. Open the package README named in the `.nuspec`, the release notes if present, and the XML documentation beside the target assembly when available. This works with custom NuGet cache locations and transitive dependencies.
3. Check the installed signatures and the application's own registration, settings and call sites before applying examples. These skills describe the framework under development. Older packages can differ in overloads, defaults and analyzer IDs. If local evidence is insufficient, use documentation for that exact version or the repository commit recorded in its package metadata. State which behavior remains unverified.

In a framework source checkout, use the repository profile or discover the owning projects locally. In a consuming application, its own project files and CI define commands and conventions. Example entity, context and program names stand for application-owned types.

## Framework Stack

| When working on | Start with |
|---|---|
| Contracts, IDs, entities, exceptions, analyzers | [SharedKernel](../drn-sharedkernel/SKILL.md). Contract projects can depend on this package alone. |
| Aggregates, DTO mapping, repository boundaries | [Domain design](../drn-domain-design/SKILL.md) |
| DI, settings, diagnostics, HTTP, cancellation, ID utilities | [Utils](../drn-utils/SKILL.md) |
| Persistence, migrations, repositories, PostgreSQL | [EntityFramework](../drn-entityframework/SKILL.md) |
| Host lifecycle, endpoints, security, Razor, static assets | [Hosting](../drn-hosting/SKILL.md) |
| DTT, data attributes, contexts, integration hosts | [Testing](../drn-testing/SKILL.md) |
| Jobs or messaging | The installed Jobs or MassTransit package and the application's registration |

## DRN Framework Maintenance Reference

Register modules through their extensions and DRN lifetime attributes. Avoid duplicate manual registrations. Validate each provider's own scope. SharedKernel owns JSON and exception semantics. Hosting owns HTTP responses. Check the application's composition root for module and startup-hook usage.

### Maintenance Reference: Configuration Sources

`AddDrnSettings` applies later sources over earlier ones:

1. `appsettings.json`, then `appsettings.{Environment}.json`.
2. User secrets when the application assembly is available.
3. Environment variables with `ASPNETCORE_`, then `DOTNET_`, then unprefixed keys.
4. Mounted key-per-file and JSON settings under `/appconfig`. Override the root through `IMountedSettingsConventionsOverride`.
5. Command-line arguments.

Set `Environment` to Development, Staging or Production before loading environment-specific settings. Missing or unknown values fail startup. App data roots resolve earlier. Use the [Utils skill](../drn-utils/SKILL.md) for those roots, explicit application and instance IDs, and key rotation.

### Maintenance Reference: Migration And Prototype Invariants

Production and Staging require explicit connection strings. Production never auto-migrates. Staging migrates only with `AutoMigrateStaging` and cannot recreate a prototype. Development defaults to automatic migration. Follow [EntityFramework](../drn-entityframework/SKILL.md#prototype-mode) for prototype, seeding and connection safeguards.

Use explicit `TemporaryApplication`, `SkipValidation` or a dedicated test context. Keep production invariants active. [Hosting](../drn-hosting/SKILL.md#drnprogrambase-pattern) owns temporary-host behavior.

### Testing and maintenance

Follow repository execution gates and [Testing](../drn-testing/SKILL.md) for settings and container setup. PostgreSQL binding does not start RabbitMQ.

Update owning package docs and affected skills when public behavior changes. Search old names, defaults and examples for drift. Follow the current repository profile for release and packaging rules. Run `git diff --check` after documentation edits.
