---
name: overview-drn-framework
description: "Route DRN framework work to version-matched package documentation and skills. Covers configuration, environment safety, and documentation ownership."
last-updated: 2026-11-07
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

Register modules through their service-collection extensions. Use DRN lifetime attributes and avoid duplicate manual registrations. Scoped validation belongs to the provider being validated. SharedKernel owns JSON conventions and exception semantics. Hosting owns the public HTTP response. Find the application's composition root to see which modules and startup hooks it uses.

### Maintenance Reference: Configuration Sources

`AddDrnSettings` applies later sources over earlier ones:

1. `appsettings.json`, then `appsettings.{Environment}.json`.
2. User secrets when the application assembly is available.
3. Environment variables with `ASPNETCORE_`, then `DOTNET_`, then unprefixed keys.
4. Mounted key-per-file and JSON settings under `/appconfig`. Override the root through `IMountedSettingsConventionsOverride`.
5. Command-line arguments.

Set `Environment` to Development, Staging or Production before loading environment-specific settings. Missing or unknown values fail startup. App data roots resolve earlier. Use the [Utils skill](../drn-utils/SKILL.md) for those roots, explicit application and instance IDs, and key rotation.

### Maintenance Reference: Migration And Prototype Invariants

Production and Staging require explicit connection strings. Production never auto-migrates. Staging migrates only with `AutoMigrateStaging` and cannot recreate a prototype. Development defaults to automatic migration. The [EntityFramework skill](../drn-entityframework/SKILL.md#prototype-mode) owns every condition for prototype recreation, retryable seeding and the connection modes.

Use explicit `TemporaryApplication`, `SkipValidation` or a dedicated test context for test-specific behavior. Keep production invariants active. A temporary host builds the pipeline for discovery, skips endpoint validation/accessor population, performs service validation unless skipped and returns before startup.

### Testing and maintenance

Follow the current repository's execution gates. `SettingsProvider` resolves settings locally or under `Settings/`. Read container defaults from the installed Testing package. PostgreSQL binding and RabbitMQ startup are separate operations.

For changes to public behavior, update the owning package documentation and affected skills. Search old names, defaults and examples for drift. Use the publisher's profile for release triggers, format, coordinated version alignment and packaging limits. A consuming repository follows its own release policy. Run `git diff --check` after documentation edits.
