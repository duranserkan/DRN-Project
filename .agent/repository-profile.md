# Repository Profile - DRN-Project

> Local overlay for this repository. `AGENTS.md` and generic skills stay portable; DRN framework skills stay reusable across repositories.

## Project Overview

| Aspect | Detail |
|--------|--------|
| Type | .NET 10 framework plus DDD reference application |
| Architecture | Domain -> Infrastructure/Application -> Hosted |
| Frontend | Razor Pages + htmx + Bootstrap 5 with Vite-built assets |
| Testing | DTT: integration-first with Testcontainers |

## Repository Layout

Physical project folders are flat at the repository root; `DRN.slnx` groups them logically for IDE navigation.

| Area | Physical folders | Solution folder |
|------|------------------|-----------------|
| Framework packages | `DRN.Framework.*` | `/Src/Framework/` |
| Nexus service | `DRN.Nexus.*` | `/Src/Nexus/` |
| Reference app | `Sample.*` | `/Src/Sample/` |
| Tests | `DRN.Test.*` | `/Test/` |
| Infrastructure | `Docker/`, root `docker-compose.yml`, `docker-compose.dcproj` | `/Docker/` |
| Agent and CI control plane | `.agent/`, `.github/workflows/`, `.github/actions/` | `/Items/` |
| Repository docs | `README.md`, `ROADMAP.md`, `SECURITY.md`, `NOTICE.md`, `docs/` | `/Docs/` |

Primary hosted projects are `Sample.Hosted/` for the reference application and `DRN.Nexus.Hosted/` for Nexus. Framework package documentation lives inside each `DRN.Framework.*` folder as `README.md`, `RELEASE-NOTES.md`, and package metadata where present.

## Skill Routing

- DRN framework work: load `overview-drn-framework`, then the relevant `drn-*` skill.
- Testing work: load `drn-testing` and `overview-drn-testing` before the generic `test-*` router skills.
- Frontend work: load the relevant `frontend-*` skill; this repository's frontend package root is `Sample.Hosted/`.
- Documentation work for framework packages: use `.agent/workflows/documentation.md`.

### Framework Skill Load Set

Framework-scoped DRN skills that should be loaded when a workflow asks for all local skills or the repository uses DRN Framework:

- `overview-drn-framework`
- `overview-drn-testing`
- `drn-sharedkernel`
- `drn-entityframework`
- `drn-domain-design`
- `drn-utils`
- `drn-hosting`
- `drn-testing`

Framework overview skills:

- `overview-drn-framework`
- `overview-drn-testing`

Framework testing skills:

- `drn-testing`
- `overview-drn-testing`

## Documentation Modules

The `/documentation` workflow targets these package modules when no narrower module is requested:

- `DRN.Framework.SharedKernel`
- `DRN.Framework.Utils`
- `DRN.Framework.EntityFramework`
- `DRN.Framework.Hosting`
- `DRN.Framework.Testing`
- `DRN.Framework.Jobs`
- `DRN.Framework.MassTransit`

## Key Commands

Do not run build or test commands unless the user explicitly allows it.

```bash
# Build solution
dotnet build DRN.slnx

# Run unit tests first
dotnet run --project DRN.Test.Unit/DRN.Test.Unit.csproj

# Run integration tests only after unit tests pass
dotnet run --project DRN.Test.Integration/DRN.Test.Integration.csproj
```

## Repository Conventions

- DI: attribute-based; do not manually register attribute-decorated services.
  - `[Scoped<T>]`, `[Singleton<T>]`, `[Transient<T>]` declare service lifetime.
  - `[Config("Section")]`, `[ConfigRoot]` bind configuration.
  - `[HostedService]` declares background services.
- Entities: Source-Known ID pattern with `long` internal IDs and `Guid` external IDs; `[EntityType(byte)]` is required on every `SourceKnownEntity`.
- DTOs: derive from `Dto`, live in `*.Contract`, and expose `Guid` IDs only. APIs return DTOs, never entities.
- Testing: use `[Fact]` for tests without inline data or generated parameters. Use `[DataInline]` / `[DataInlineUnit]` for inline data, `[DataMember]` / `[DataMemberUnit]` for member data, and `DataSelfAttribute` / `DataSelfUnitAttribute` for generated self data. Request `DrnTestContext` / `DrnTestContextUnit` only when the test needs the context; inline/member/self data follows the optional context and AutoFixture/NSubstitute fill remaining parameters. Unit tests are listed before integration tests. Run MTP test projects with `dotnet run --project <test-csproj>` and do not use `.slnx` in test-run commands. Keep reusable `WebApplicationFactory<TProgram>` entry-point programs in non-test support assemblies such as `DRN.Test.Utils`, not inside the MTP test executable.
- Frontend:
  - Vite source lives in `Sample.Hosted/buildwww/`; built assets live under `Sample.Hosted/wwwroot/`.
  - Browser utilities are exposed under `window.DRN`; Vite config imports `buildwww/app/js/drn/drnUtils.js` as `drnUtils`.
  - `DRN`, `Drn*`, and `DRN.Framework.*` identifiers are shared framework surface, not repository-local placeholders; do not replace them with app-specific names.
  - React islands use `buildwww/types/DrnReactTypes.ts`, `.drn-react-root`, and the `DrnReactMicroFrontend` IIFE wrapper name.
  - CSP nonces are auto-injected via `NonceTagHelper`; CSRF is auto-added on `hx-post`, `hx-put`, `hx-delete`, and `hx-patch`.
- Git: GitFlow-inspired. Merge `develop` to `master`; release tags use `release/v*.*.*` or `release/v*.*.*-previewNNN`. Squash merge to `develop`; use merge commits to `master`.
- CI/CD: PR workflows use split checkout: trusted workflow/composite action code from `github.event.pull_request.base.sha` at the workspace root and PR source under `src`. PR jobs remain secretless, have explicit `timeout-minutes`, and are joined by an aggregate gatekeeper. Release workflows derive one version from `release/v...` tags, verify exact protected-branch ancestry with `git merge-base --is-ancestor`, keep build artifacts in the publishing job unless explicit artifacts cross job boundaries, stage Docker images by digest, scan the exact staged digest, then publish packages and promote Docker tags.
- Security: CSP nonces, CSRF anti-forgery, and input validation are mandatory. Third-party GitHub Actions must stay pinned to full commit SHAs with version comments, and tag-to-SHA mapping must be verified from the official upstream action repository. `.github/CODEOWNERS` owns itself, workflows, and composite actions; branch rulesets should require code-owner review and required code-scanning/gatekeeper statuses. CodeRabbit global rules are sourced through `knowledge_base.code_guidelines.filePatterns` for `AGENTS.md` and `.agent/rules/DiSCOS.md`. See `basic-security-checklist`, `overview-github-actions`, and `drn-hosting`.
- Containers: Docker SDK/runtime distro choices and `DOTNET_RUNTIME_VERSION` must stay aligned with `RuntimeFrameworkVersion` metadata.

## DRN Framework Source Overlay

Framework-level conventions and default values live in the framework-scoped DRN skills, especially `overview-drn-framework`. This profile records the source-owned files in this repository that should be checked before changing those framework facts.

### Source Map

| Shared fact | Source of truth | Main consumers |
|-------------|-----------------|----------------|
| Configuration source order | [ConfigurationExtensions.cs](../DRN.Framework.Hosting/Extensions/ConfigurationExtensions.cs) | Utils README, Hosting README, DRN framework skills |
| Mounted settings paths | [MountedSettingsConventions.cs](../DRN.Framework.Utils/Settings/Conventions/MountedSettingsConventions.cs) | Utils README, Hosting README |
| Development settings defaults | [DrnDevelopmentSettings.cs](../DRN.Framework.Utils/Settings/DrnDevelopmentSettings.cs) | EntityFramework README, Testing README |
| Test settings convention | [SettingsProvider.cs](../DRN.Framework.Testing/Providers/SettingsProvider.cs) | Testing README, testing skills |
| Vite manifest discovery and publish support | [ViteManifest.cs](../DRN.Framework.Hosting/Utils/Vite/ViteManifest.cs), [DRN.Framework.Hosting.targets](../DRN.Framework.Hosting/buildTransitive/DRN.Framework.Hosting.targets) | Hosting README, frontend Vite skill, DRN hosting skill |

### Documentation Sync Checklist

When source code changes one of the shared framework facts above:

1. Verify the source-owned file in the source map.
2. Update package READMEs with self-contained important facts wherever package readers need the new behavior.
3. Update release notes when a published behavior or user-facing default changed.
4. Update the framework-scoped DRN skills that agents use for that package.
5. Search changed terms, renamed keys, changed defaults, and removed examples across package docs, framework skills, `AGENTS.md`, and this profile.
6. Run `git diff --check`.
